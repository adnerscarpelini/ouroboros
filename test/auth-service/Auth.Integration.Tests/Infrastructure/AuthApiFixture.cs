namespace Ouroboros.Auth.Integration.Tests.Infrastructure;

using System.Net;
using Dapper;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

// Um PostgreSQL descartavel e uma API em memoria por execucao, compartilhados pela collection.
public sealed class AuthApiFixture : IAsyncLifetime
{
    public const string TrustedProxyIp = "10.0.0.1";

    // Limites baixos pra estourar o 429 com poucas requisicoes.
    public const int LowPermitLimit = 3;

    private static int _nextIp;

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
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

    // O limite por IP fica em memoria e nao e zerado entre testes: cada teste usa IPs proprios.
    public static string NextIp()
    {
        var value = Interlocked.Increment(ref _nextIp);
        return new IPAddress([10, 1, (byte)(value / 250), (byte)(value % 250 + 1)]).ToString();
    }

    public async Task ResetDatabaseAsync()
    {
        const string sql = """
            DO $$
            DECLARE tables text;
            BEGIN
                SELECT string_agg(format('%I.%I', schemaname, tablename), ', ')
                INTO tables
                FROM pg_tables
                WHERE schemaname = 'auth' AND tablename <> 'schemaversions';

                IF tables IS NOT NULL THEN
                    EXECUTE 'TRUNCATE ' || tables || ' RESTART IDENTITY CASCADE';
                END IF;
            END $$;
            """;

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.ExecuteAsync(sql);
    }
}

[CollectionDefinition(Name)]
public sealed class AuthApiCollection : ICollectionFixture<AuthApiFixture>
{
    public const string Name = "AuthApi";
}
