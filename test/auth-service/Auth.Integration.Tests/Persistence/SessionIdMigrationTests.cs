namespace Ouroboros.Auth.Integration.Tests.Persistence;

using Dapper;
using DbUp;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Infrastructure.Migrations;
using Ouroboros.Auth.Infrastructure.Persistence;
using Ouroboros.Auth.Integration.Tests.Infrastructure;
using Xunit;

// Spec 2026092506: a migration de session_id num banco que ja tinha refresh tokens.
[Collection(AuthApiCollection.Name)]
public sealed class SessionIdMigrationTests
{
    private const string MigrationName = "AddSessionIdToRefreshTokens";

    private readonly AuthApiFixture _fixture;

    public SessionIdMigrationTests(AuthApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ShouldGiveEachExistingRefreshTokenItsOwnSessionAndMakeTheColumnRequired()
    {
        var databaseName = $"auth_migration_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(_fixture.ConnectionString) { InitialCatalog = databaseName }.ConnectionString;

        EnsureDatabase.For.SqlDatabase(connectionString);

        try
        {
            // Banco no estado anterior a spec: todas as migrations, menos a do session_id.
            var before = DeployChanges.To
                .SqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(typeof(MigrationRunner).Assembly, name => !name.Contains(MigrationName))
                .Build()
                .PerformUpgrade();

            Assert.True(before.Successful);

            var firstUser = await AddUserAsync(connectionString, "migration.first");
            var secondUser = await AddUserAsync(connectionString, "migration.second");

            await using (var connection = new SqlConnection(connectionString))
            {
                foreach (var (owner, hash) in new[] { (firstUser, "hash-1"), (firstUser, "hash-2"), (secondUser, "hash-3") })
                {
                    await connection.ExecuteAsync(
                        """
                        INSERT INTO auth.refresh_tokens (external_id, created_at, user_id, token_hash, expires_at)
                        SELECT NEWID(), SYSDATETIMEOFFSET(), users.id, @Hash, DATEADD(DAY, 7, SYSDATETIMEOFFSET())
                        FROM auth.users AS users
                        WHERE users.external_id = @Owner;
                        """,
                        new { Owner = owner, Hash = hash });
                }
            }

            MigrationRunner.Run(connectionString);

            await using var after = new SqlConnection(connectionString);
            var sessions = (await after.QueryAsync<Guid>("SELECT session_id FROM auth.refresh_tokens;")).ToList();
            var nullable = await after.ExecuteScalarAsync<string>(
                "SELECT is_nullable FROM sys.columns WHERE object_id = OBJECT_ID('auth.refresh_tokens') AND name = 'session_id';");
            var indexColumns = (await after.QueryAsync<string>(
                """
                SELECT columns.name
                FROM sys.indexes AS indexes
                INNER JOIN sys.index_columns AS indexColumns ON indexColumns.object_id = indexes.object_id AND indexColumns.index_id = indexes.index_id
                INNER JOIN sys.columns AS columns ON columns.object_id = indexColumns.object_id AND columns.column_id = indexColumns.column_id
                WHERE indexes.name = 'refresh_tokens_user_id_session_id_idx'
                ORDER BY indexColumns.key_ordinal;
                """)).ToList();

            Assert.Equal(3, sessions.Count);
            Assert.Equal(3, sessions.Distinct().Count());
            Assert.DoesNotContain(Guid.Empty, sessions);
            Assert.Equal("False", nullable);
            Assert.Equal(["user_id", "session_id"], indexColumns);
        }
        finally
        {
            await using var master = new SqlConnection(_fixture.ConnectionString);
            await master.ExecuteAsync($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    private static async Task<Guid> AddUserAsync(
        string connectionString,
        string login)
    {
        var user = User.Create(login, "Migration Test", $"{login}@example.com", "hash");

        await using var session = new DbSession(new SqlConnectionFactory(connectionString));
        await new DapperUserRepository(session, NullLogger<DapperUserRepository>.Instance).AddAsync(user);

        return user.ExternalId;
    }
}
