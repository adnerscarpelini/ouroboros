namespace Ouroboros.Auth.Integration.Tests.Infrastructure;

using System.Net;
using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Ouroboros.Auth.Infrastructure.Persistence;
using Testcontainers.MsSql;
using Xunit;

// Um SQL Server descartavel e uma API em memoria por execucao, compartilhados pela collection.
public sealed class AuthApiFixture : IAsyncLifetime
{
    public const string TrustedProxyIp = "10.0.0.1";

    // Limites baixos pra estourar o 429 com poucas requisicoes.
    public const int LowPermitLimit = 3;

    public static readonly string[] RateLimitedPolicies =
    [
        "auth-login",
        "auth-refresh",
        "email-confirm",
        "user-register",
        "user-delete",
        "password-reset-request",
        "password-reset-confirm",
    ];

    private static int _nextIp;

    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public AuthApiFactory Factory { get; private set; } = null!;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        Factory = new AuthApiFactory(ConnectionString);

        // Sobe a API ja aqui: as migrations rodam no startup.
        Factory.CreateClient().Dispose();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    public HttpClient CreateClient() => Factory.CreateClient();

    // API em que o N-esimo uso de um metodo do gateway falha. Quem chama descarta a factory.
    public WebApplicationFactory<Program> CreateFactoryFailingOn<TGateway>(
        string method,
        FaultTiming timing = FaultTiming.Before,
        int onCall = 1)
        where TGateway : class
    {
        return Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.FailOn<TGateway>(method, timing, onCall)));
    }

    // Sessao propria pra o teste preparar e conferir dados direto no banco, fora da API.
    public DbSession CreateSession() => new(new SqlConnectionFactory(ConnectionString));

    // O limite por IP fica em memoria e nao e zerado entre testes: cada teste usa IPs proprios.
    public static string NextIp()
    {
        var value = Interlocked.Increment(ref _nextIp);
        return new IPAddress([10, 1, (byte)(value / 250), (byte)(value % 250 + 1)]).ToString();
    }

    public async Task ResetDatabaseAsync()
    {
        const string sql = """
            DELETE FROM auth.refresh_tokens;
            DELETE FROM auth.tokens;
            DELETE FROM auth.users;

            DBCC CHECKIDENT ('auth.refresh_tokens', RESEED, 0) WITH NO_INFOMSGS;
            DBCC CHECKIDENT ('auth.tokens', RESEED, 0) WITH NO_INFOMSGS;
            DBCC CHECKIDENT ('auth.users', RESEED, 0) WITH NO_INFOMSGS;
            """;

        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(sql);
    }
}

[CollectionDefinition(Name)]
public sealed class AuthApiCollection : ICollectionFixture<AuthApiFixture>
{
    public const string Name = "AuthApi";
}
