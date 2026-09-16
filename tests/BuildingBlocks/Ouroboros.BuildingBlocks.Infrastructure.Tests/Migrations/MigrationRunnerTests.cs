using Npgsql;
using Testcontainers.PostgreSql;

namespace Ouroboros.BuildingBlocks.Infrastructure.Tests.Migrations;

// Cada teste sobe seu próprio Postgres efêmero: os cenários testam o próprio ciclo de vida do
// schema_migrations (primeira aplicação, reexecução, checksum divergente, baseline do EF), então
// não podem compartilhar estado entre si.
[Trait("Category", "Integration")]
public sealed class MigrationRunnerTests : IAsyncLifetime
{
	private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17")
		.Build();

	private NpgsqlConnection _connection = null!;

	public async Task InitializeAsync()
	{
		await _container.StartAsync();
		_connection = new NpgsqlConnection(_container.GetConnectionString());
		await _connection.OpenAsync();
	}

	public async Task DisposeAsync()
	{
		await _connection.DisposeAsync();
		await _container.DisposeAsync();
	}

	[Fact]
	public async Task RunAsync_applies_pending_migrations_and_records_them_in_history()
	{
		var runner = new MigrationRunner(_connection);
		var migrations = new[]
		{
			new MigrationFile("20260101000000", "CreateA", "a.sql", "create table a (id int);", FakeChecksum("a")),
			new MigrationFile("20260102000000", "CreateB", "b.sql", "create table b (id int);", FakeChecksum("b"))
		};

		var result = await runner.RunAsync(migrations, dryRun: false, CancellationToken.None);

		Assert.Equal(2, result.AppliedCount);
		Assert.Equal(2, await CountAppliedMigrationsAsync());
	}

	[Fact]
	public async Task RunAsync_does_not_reapply_migrations_already_recorded_in_history()
	{
		var runner = new MigrationRunner(_connection);
		var migrations = new[]
		{
			new MigrationFile("20260101000000", "CreateA", "a.sql", "create table a (id int);", FakeChecksum("a"))
		};

		await runner.RunAsync(migrations, dryRun: false, CancellationToken.None);
		var second = await runner.RunAsync(migrations, dryRun: false, CancellationToken.None);

		Assert.Equal(0, second.AppliedCount);
		Assert.Equal(1, await CountAppliedMigrationsAsync());
	}

	[Fact]
	public async Task RunAsync_dry_run_reports_pending_migrations_without_applying_them()
	{
		var runner = new MigrationRunner(_connection);
		var migrations = new[]
		{
			new MigrationFile("20260101000000", "CreateA", "a.sql", "create table a (id int);", FakeChecksum("a"))
		};

		var result = await runner.RunAsync(migrations, dryRun: true, CancellationToken.None);

		Assert.Equal(0, result.AppliedCount);
		Assert.Equal(1, result.PendingCount);
		await using var command = new NpgsqlCommand(
			"""
			SELECT
				to_regclass('a') IS NOT NULL;
			""",
			_connection);
		Assert.False((bool)(await command.ExecuteScalarAsync() ?? false));
	}

	[Fact]
	public async Task RunAsync_throws_when_an_applied_migration_file_changes()
	{
		var runner = new MigrationRunner(_connection);
		var original = new MigrationFile("20260101000000", "CreateA", "a.sql", "create table a (id int);", FakeChecksum("a"));
		await runner.RunAsync([original], dryRun: false, CancellationToken.None);

		var tampered = original with { Checksum = FakeChecksum("diferente") };

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			runner.RunAsync([tampered], dryRun: false, CancellationToken.None));
	}

	[Fact]
	public async Task RunAsync_throws_when_the_database_has_an_ef_history_table_but_no_baseline()
	{
		await using (var command = new NpgsqlCommand(
			"""
			CREATE TABLE public."__EFMigrationsHistory" (
			    "MigrationId" varchar(150) primary key,
			    "ProductVersion" varchar(32) not null
			);
			""",
			_connection))
		{
			await command.ExecuteNonQueryAsync();
		}

		var runner = new MigrationRunner(_connection);
		var migrations = new[]
		{
			new MigrationFile("20260101000000", "CreateA", "a.sql", "create table a (id int);", FakeChecksum("a"))
		};

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			runner.RunAsync(migrations, dryRun: false, CancellationToken.None));
	}

	// A coluna checksum é char(64): o Postgres preenche valores mais curtos com espaços até o
	// tamanho fixo, e o valor lido de volta vem com esse preenchimento. Um checksum de teste
	// precisa ter exatamente 64 caracteres para não divergir do que foi originalmente gravado.
	private static string FakeChecksum(string seed)
	{
		return seed.PadRight(64, '0');
	}

	private async Task<long> CountAppliedMigrationsAsync()
	{
		await using var command = new NpgsqlCommand(
			"""
			SELECT
				count(*)
			FROM
				public.schema_migrations;
			""",
			_connection);

		return (long)(await command.ExecuteScalarAsync() ?? 0L);
	}
}
