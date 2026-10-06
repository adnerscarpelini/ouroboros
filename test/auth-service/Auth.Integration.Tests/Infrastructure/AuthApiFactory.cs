namespace Ouroboros.Auth.Integration.Tests.Infrastructure;

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ouroboros.Auth.Application.Gateways;

public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    // O TestServer nao tem conexao TCP: o teste escolhe o IP de origem por este header.
    public const string RemoteIpHeader = "X-Test-Remote-Ip";

    private readonly string _connectionString;
    private readonly IReadOnlyList<(string Kid, string PrivateKeyPath, string Status)> _signingKeys;

    public FakeBreachedPasswordChecker BreachedPasswordChecker { get; } = new();

    // signingKeys: por padrao as duas chaves de teste (a Active assina, a Published so valida). Os testes de rotacao e de
    // validacao do startup passam as suas.
    public AuthApiFactory(
        string connectionString,
        IReadOnlyList<(string Kid, string PrivateKeyPath, string Status)>? signingKeys = null)
    {
        _connectionString = connectionString;
        _signingKeys =
            signingKeys
            ??
            [
                (TestSigningKeys.ActiveKid, TestSigningKeys.PemPath(TestSigningKeys.ActiveKid), "Active"),
                (TestSigningKeys.PublishedKid, TestSigningKeys.PemPath(TestSigningKeys.PublishedKid), "Published"),
            ];
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting pra valer ja no startup, onde o Program le a connection string.
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        TestSigningKeys.EnsureFilesExist();

        for (var index = 0; index < _signingKeys.Count; index++)
        {
            builder.UseSetting($"Jwt:SigningKeys:{index}:Kid", _signingKeys[index].Kid);
            builder.UseSetting($"Jwt:SigningKeys:{index}:PrivateKeyPath", _signingKeys[index].PrivateKeyPath);
            builder.UseSetting($"Jwt:SigningKeys:{index}:Status", _signingKeys[index].Status);
        }
        builder.UseSetting("Seq:ServerUrl", string.Empty);

        // A limpeza de tokens expirados fica desligada: so os testes dela (TokenCleanupTests) a ligam.
        builder.UseSetting("TokenCleanup:Enabled", "false");
        builder.UseSetting("ForwardedHeaders:KnownProxies:0", AuthApiFixture.TrustedProxyIp);

        foreach (var policy in AuthApiFixture.RateLimitedPolicies)
        {
            builder.UseSetting($"RateLimiting:{policy}:PermitLimit", AuthApiFixture.LowPermitLimit.ToString());
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStartupFilter, RemoteIpStartupFilter>();

            // Sem rede nos testes: o Pwned Passwords e trocado por um fake que so conhece as senhas informadas.
            services.RemoveAll<IBreachedPasswordChecker>();
            services.AddSingleton<IBreachedPasswordChecker>(BreachedPasswordChecker);
        });
    }

    // Roda antes de todo o pipeline do Program, inclusive do UseForwardedHeaders.
    private sealed class RemoteIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use((context, nextMiddleware) =>
                {
                    if (IPAddress.TryParse(context.Request.Headers[RemoteIpHeader], out var address))
                    {
                        context.Connection.RemoteIpAddress = address;
                    }

                    return nextMiddleware(context);
                });

                next(app);
            };
        }
    }
}
