namespace Ouroboros.Auth.Integration.Tests.Persistence;

using System.Diagnostics;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Infrastructure.Migrations;
using Ouroboros.Auth.Infrastructure.Persistence;
using Ouroboros.Auth.Integration.Tests.Infrastructure;
using Xunit;

// Spec 2026092511: papeis de banco separados (auth_migrator faz DDL, auth_service so DML) e o comando "migrate".
// O SQL dos papeis e o do proprio docker/sqlserver/init/01-create-auth-db.sh, extraido do bloco EOSQL.
[Collection(AuthApiCollection.Name)]
public sealed class DatabaseRolesAndMigrateTests
{
    private const string MigratorPassword = "Migrator-Password-1234!";
    private const string ServicePassword = "Service-Password-1234!";

    private readonly AuthApiFixture _fixture;

    public DatabaseRolesAndMigrateTests(AuthApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ShouldLetTheMigratorRunDdlAndTheServiceOnlyDml()
    {
        await using var database = await RolesDatabase.CreateAsync(_fixture);

        // O migrator e dono do schema: aplica todas as migrations, inclusive o CREATE SCHEMA que ja existe.
        MigrationRunner.Run(database.MigratorConnectionString);

        await using var session = new DbSession(new SqlConnectionFactory(database.ServiceConnectionString));
        var users = new DapperUserRepository(session, NullLogger<DapperUserRepository>.Instance);
        var user = User.Create("roles.user", "Roles Test", "roles.user@example.com", "hash");

        // DML nas tabelas criadas pelo migrator: INSERT, SELECT, UPDATE e DELETE.
        await users.AddAsync(user);
        Assert.NotNull(await users.GetByExternalIdAsync(user.ExternalId));
        user.ConfirmEmail();
        await users.UpdateAsync(user);
        await users.RemoveAsync(user.ExternalId);
        Assert.Null(await users.GetByExternalIdAsync(user.ExternalId));

        // O lock de aplicacao da contagem de Admins tambem roda sem privilegio de DDL.
        await new SqlUnitOfWork(session).ExecuteAsync(async () => await users.CountActiveAdminsForUpdateAsync());
    }

    [Theory]
    [InlineData("CREATE TABLE auth.evil (id int);")]
    [InlineData("CREATE TABLE dbo.evil (id int);")]
    [InlineData("ALTER TABLE auth.users ADD evil int NULL;")]
    [InlineData("DROP TABLE auth.refresh_tokens;")]
    [InlineData("CREATE INDEX evil_idx ON auth.users (full_name);")]
    [InlineData("TRUNCATE TABLE auth.tokens;")]
    [InlineData("CREATE SCHEMA evil;")]
    [InlineData("DROP SCHEMA auth;")]
    [InlineData("GRANT SELECT ON SCHEMA::auth TO public;")]
    public async Task ShouldRefuseDdlToTheServiceRole(string ddl)
    {
        await using var database = await RolesDatabase.CreateAsync(_fixture);
        MigrationRunner.Run(database.MigratorConnectionString);

        await using var service = new SqlConnection(database.ServiceConnectionString);

        var error = await Assert.ThrowsAsync<SqlException>(() => service.ExecuteAsync(ddl));

        Assert.Contains(error.Number, new[] { 262, 1088, 3701, 4902, 15151, 2760, 15247, 15248 });
    }

    [Fact]
    public async Task ShouldGiveTheServiceDmlOnTablesCreatedAfterTheGrant()
    {
        await using var database = await RolesDatabase.CreateAsync(_fixture);
        MigrationRunner.Run(database.MigratorConnectionString);

        await using var migrator = new SqlConnection(database.MigratorConnectionString);
        await migrator.ExecuteAsync("CREATE TABLE auth.future_table (id bigint IDENTITY(1,1) PRIMARY KEY, name nvarchar(50) NOT NULL);");

        await using var service = new SqlConnection(database.ServiceConnectionString);
        await service.ExecuteAsync("INSERT INTO auth.future_table (name) VALUES (N'one');");
        await service.ExecuteAsync("UPDATE auth.future_table SET name = N'two';");

        Assert.Equal("two", await service.ExecuteScalarAsync<string>("SELECT name FROM auth.future_table;"));
        Assert.Equal(1, await service.ExecuteAsync("DELETE FROM auth.future_table;"));
    }

    [Fact]
    public async Task ShouldLetTheServiceInsertAndReadAuditEventsButNeverUpdateOrDeleteThem()
    {
        // Spec 2026092519: a trilha e so de insercao, garantida pelo banco (DENY no objeto, sobre o GRANT no schema).
        await using var database = await RolesDatabase.CreateAsync(_fixture);
        MigrationRunner.Run(database.MigratorConnectionString);

        await using var service = new SqlConnection(database.ServiceConnectionString);
        await service.ExecuteAsync(
            "INSERT INTO auth.audit_events (external_id, occurred_at, event_type, outcome, reason) VALUES (NEWID(), SYSDATETIMEOFFSET(), N'LoginFailed', N'Failure', N'invalid_password');");

        Assert.Equal(1, await service.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM auth.audit_events;"));

        var update = await Assert.ThrowsAsync<SqlException>(() => service.ExecuteAsync("UPDATE auth.audit_events SET reason = N'tampered';"));
        var delete = await Assert.ThrowsAsync<SqlException>(() => service.ExecuteAsync("DELETE FROM auth.audit_events;"));
        var truncate = await Assert.ThrowsAsync<SqlException>(() => service.ExecuteAsync("TRUNCATE TABLE auth.audit_events;"));

        Assert.Equal(229, update.Number);
        Assert.Equal(229, delete.Number);
        Assert.Contains(truncate.Number, new[] { 1088, 4701, 229 });
        Assert.Equal(1, await service.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM auth.audit_events WHERE reason = N'invalid_password';"));
    }

    [Fact]
    public async Task ShouldLetTheMigratorRunTheRetentionDeleteOnOldAuditEvents()
    {
        await using var database = await RolesDatabase.CreateAsync(_fixture);
        MigrationRunner.Run(database.MigratorConnectionString);

        await using var migrator = new SqlConnection(database.MigratorConnectionString);
        await migrator.ExecuteAsync(
            """
            INSERT INTO auth.audit_events (external_id, occurred_at, event_type, outcome) VALUES (NEWID(), DATEADD(DAY, -400, SYSDATETIMEOFFSET()), N'LoginSucceeded', N'Success');
            INSERT INTO auth.audit_events (external_id, occurred_at, event_type, outcome) VALUES (NEWID(), DATEADD(DAY, -10, SYSDATETIMEOFFSET()), N'LoginSucceeded', N'Success');
            """);

        var removed = await migrator.ExecuteAsync("DELETE FROM auth.audit_events WHERE occurred_at < DATEADD(YEAR, -1, SYSDATETIMEOFFSET());");

        Assert.Equal(1, removed);
        Assert.Equal(1, await migrator.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM auth.audit_events;"));
    }

    [Fact]
    public async Task ShouldNotApplyMigrationsWhenTheApiStarts()
    {
        await using var database = await RolesDatabase.CreateAsync(_fixture);

        await using var factory = new AuthApiFactory(database.ServiceConnectionString);
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.True(response.IsSuccessStatusCode);
        await using var connection = new SqlConnection(database.MigratorConnectionString);
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name = 'users';"));
    }

    [Fact]
    public async Task ShouldApplyMigrationsAndExitWhenTheApiIsCalledWithMigrate()
    {
        await using var database = await RolesDatabase.CreateAsync(_fixture);

        var result = await RunApiAsync("migrate", ("ConnectionStrings__Migration", database.MigratorConnectionString));

        Assert.Equal(0, result.ExitCode);
        await using var connection = new SqlConnection(database.MigratorConnectionString);
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name = 'users';"));
        Assert.True(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions;") >= 9);
    }

    [Fact]
    public async Task ShouldBeIdempotentWhenMigrateRunsTwice()
    {
        await using var database = await RolesDatabase.CreateAsync(_fixture);

        var first = await RunApiAsync("migrate", ("ConnectionStrings__Migration", database.MigratorConnectionString));
        var second = await RunApiAsync("migrate", ("ConnectionStrings__Migration", database.MigratorConnectionString));

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(0, second.ExitCode);
    }

    [Fact]
    public async Task ShouldFailWhenMigrateHasNoMigrationConnectionString()
    {
        var result = await RunApiAsync("migrate");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Migration", result.Output);
    }

    private static async Task<(int ExitCode, string Output)> RunApiAsync(
        string argument,
        params (string Name, string Value)[] environment)
    {
        var startInfo = new ProcessStartInfo("dotnet", $"\"{Path.Combine(AppContext.BaseDirectory, "Auth.Api.dll")}\" {argument}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // Sem a configuracao do ambiente de desenvolvimento de quem roda os testes.
        startInfo.Environment["ConnectionStrings__Default"] = string.Empty;
        startInfo.Environment.Remove("ConnectionStrings__Migration");

        foreach (var (name, value) in environment)
        {
            startInfo.Environment[name] = value;
        }

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));

        return (process.ExitCode, await output + await error);
    }

    // Banco descartavel no container, com os papeis criados pelo SQL real do script de init.
    private sealed class RolesDatabase : IAsyncDisposable
    {
        private readonly AuthApiFixture _fixture;
        private readonly string _database;
        private readonly string _migratorLogin;
        private readonly string _serviceLogin;

        private RolesDatabase(
            AuthApiFixture fixture,
            string suffix)
        {
            _fixture = fixture;
            _database = $"auth_roles_{suffix}";
            _migratorLogin = $"auth_migrator_{suffix}";
            _serviceLogin = $"auth_service_{suffix}";
        }

        public string MigratorConnectionString => Build(_migratorLogin, MigratorPassword);

        public string ServiceConnectionString => Build(_serviceLogin, ServicePassword);

        public static async Task<RolesDatabase> CreateAsync(AuthApiFixture fixture)
        {
            var database = new RolesDatabase(fixture, Guid.NewGuid().ToString("N")[..12]);

            await using var connection = new SqlConnection(fixture.ConnectionString);
            await connection.OpenAsync();

            foreach (var batch in ReadInitBatches(database))
            {
                await connection.ExecuteAsync(batch);
            }

            return database;
        }

        public async ValueTask DisposeAsync()
        {
            await using var connection = new SqlConnection(_fixture.ConnectionString);
            await connection.ExecuteAsync($"""
                IF DB_ID('{_database}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{_database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{_database}];
                END;
                IF SUSER_ID('{_migratorLogin}') IS NOT NULL DROP LOGIN [{_migratorLogin}];
                IF SUSER_ID('{_serviceLogin}') IS NOT NULL DROP LOGIN [{_serviceLogin}];
                """);
        }

        private string Build(
            string login,
            string password) =>
            new SqlConnectionStringBuilder(_fixture.ConnectionString)
            {
                InitialCatalog = _database,
                UserID = login,
                Password = password,
                Pooling = false,
            }.ConnectionString;

        // Le o bloco EOSQL do script de init, troca as variaveis do sqlcmd ($(NOME)) e separa os lotes pelas linhas GO.
        private static IEnumerable<string> ReadInitBatches(RolesDatabase database)
        {
            var script = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docker", "sqlserver", "init", "01-create-auth-db.sh"));
            var start = script.IndexOf("<<'EOSQL'", StringComparison.Ordinal);
            var body = script[(script.IndexOf('\n', start) + 1)..script.LastIndexOf("EOSQL", StringComparison.Ordinal)];

            var variables = new Dictionary<string, string>
            {
                ["DB"] = database._database,
                ["MIGRATOR"] = database._migratorLogin,
                ["MIGRATORPASSWORD"] = MigratorPassword,
                ["USR"] = database._serviceLogin,
                ["DBPASSWORD"] = ServicePassword,
            };

            body = Regex.Replace(body, @"\$\((\w+)\)", match => variables[match.Groups[1].Value]);

            return Regex.Split(body.Replace("\r\n", "\n"), @"^GO\s*$", RegexOptions.Multiline)
                .Where(batch => !string.IsNullOrWhiteSpace(batch));
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Ouroboros.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("Ouroboros.slnx not found above the test output.");
        }
    }
}
