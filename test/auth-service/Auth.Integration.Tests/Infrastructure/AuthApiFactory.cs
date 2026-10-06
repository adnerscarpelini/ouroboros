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

    // Chave so de teste: deixa o teste forjar tokens (expirado, outra chave) pra exercitar o middleware JWT.
    public const string SigningKey = "integration-tests-signing-key-with-32-bytes-or-more";

    private readonly string _connectionString;

    public FakeBreachedPasswordChecker BreachedPasswordChecker { get; } = new();

    public AuthApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting pra valer ja no startup, onde o Program le a connection string.
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Seq:ServerUrl", string.Empty);
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
