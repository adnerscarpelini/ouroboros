using Npgsql;

namespace Ouroboros.BuildingBlocks.Infrastructure;

// Extraído do Ouroboros.DatabaseMigrator para ser compartilhado entre a ferramenta de linha de
// comando e os testes de integração, que precisam aplicar as mesmas migrations num Postgres efêmero.
public sealed class MigrationRunner
{
	private const string LockKey = "ouroboros.sql-migrations";
	private readonly NpgsqlConnection _connection;

	public MigrationRunner(NpgsqlConnection connection)
	{
		_connection = connection;
	}

	public async Task<MigrationRunResult> RunAsync(
		IReadOnlyList<MigrationFile> migrations,
		bool dryRun,
		CancellationToken cancellationToken
	)
	{
		var historyExists = await HistoryTableExistsAsync(cancellationToken);

		if (!historyExists && await EfHistoryTableExistsAsync(cancellationToken))
		{
			throw new InvalidOperationException(
				"O banco possui __EFMigrationsHistory, mas ainda não possui public.schema_migrations. " +
				"Faça a reconciliação/baseline das migrations SQL antes de executar o runner.");
		}

		var applied = historyExists
			? await ReadAppliedMigrationsAsync(cancellationToken)
			: new Dictionary<string, string>(StringComparer.Ordinal);

		foreach (var migration in migrations)
		{
			if (applied.TryGetValue(migration.Version, out var appliedChecksum)
				&& !string.Equals(appliedChecksum, migration.Checksum, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException(
					$"Checksum divergente na migration '{migration.Version}-{migration.Description}.sql'. " +
					"O arquivo aplicado foi alterado; crie uma nova migration.");
			}
		}

		var pending = migrations
			.Where(migration => !applied.ContainsKey(migration.Version))
			.ToArray();

		if (dryRun)
		{
			return new MigrationRunResult(0, pending);
		}

		await EnsureHistoryTableAsync(cancellationToken);
		await AcquireLockAsync(cancellationToken);

		try
		{
			var appliedCount = 0;

			foreach (var migration in pending)
			{
				await using var transaction = await _connection.BeginTransactionAsync(cancellationToken);

				await using (var command = new NpgsqlCommand(migration.Sql, _connection, transaction))
				{
					await command.ExecuteNonQueryAsync(cancellationToken);
				}

				await using (var historyCommand = new NpgsqlCommand(
					"""
					INSERT INTO public.schema_migrations (
						version,
						description,
						checksum,
						applied_at
					)
					VALUES (
						@version,
						@description,
						@checksum,
						now()
					);
					""",
					_connection,
					transaction))
				{
					historyCommand.Parameters.AddWithValue("version", migration.Version);
					historyCommand.Parameters.AddWithValue("description", migration.Description);
					historyCommand.Parameters.AddWithValue("checksum", migration.Checksum);
					await historyCommand.ExecuteNonQueryAsync(cancellationToken);
				}

				await transaction.CommitAsync(cancellationToken);
				appliedCount++;
			}

			return new MigrationRunResult(appliedCount, Array.Empty<MigrationFile>());
		}
		finally
		{
			await ReleaseLockAsync(cancellationToken);
		}
	}

	private async Task<bool> HistoryTableExistsAsync(CancellationToken cancellationToken)
	{
		await using var command = new NpgsqlCommand(
			"""
			SELECT
				to_regclass('public.schema_migrations') IS NOT NULL;
			""",
			_connection);

		return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
	}

	private async Task<bool> EfHistoryTableExistsAsync(CancellationToken cancellationToken)
	{
		// Sem a barra invertida: dentro de uma string SQL delimitada por aspas simples, aspas
		// duplas não precisam (e não podem) ser escapadas com "\" — isso manda o caractere de
		// barra invertida pro Postgres e corrompe o identificador, fazendo to_regclass nunca
		// encontrar a tabela mesmo quando ela existe.
		await using var command = new NpgsqlCommand(
			"""
			SELECT
				to_regclass('public."__EFMigrationsHistory"') IS NOT NULL;
			""",
			_connection);

		return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
	}

	private async Task<Dictionary<string, string>> ReadAppliedMigrationsAsync(CancellationToken cancellationToken)
	{
		var applied = new Dictionary<string, string>(StringComparer.Ordinal);
		await using var command = new NpgsqlCommand(
			"""
			SELECT
				version,
				checksum
			FROM
				public.schema_migrations
			ORDER BY version;
			""",
			_connection);
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);

		while (await reader.ReadAsync(cancellationToken))
		{
			applied.Add(reader.GetString(0), reader.GetString(1));
		}

		return applied;
	}

	private async Task EnsureHistoryTableAsync(CancellationToken cancellationToken)
	{
		await using var command = new NpgsqlCommand(
			"""
			CREATE SCHEMA IF NOT EXISTS public;

			CREATE TABLE IF NOT EXISTS public.schema_migrations
			(
			    version varchar(14) primary key,
			    description varchar(200) not null,
			    checksum char(64) not null,
			    applied_at timestamp with time zone not null
			);
			""",
			_connection);

		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private async Task AcquireLockAsync(CancellationToken cancellationToken)
	{
		await using var command = new NpgsqlCommand(
			"""
			SELECT
				pg_advisory_lock(hashtext(@lock_key));
			""",
			_connection);
		command.Parameters.AddWithValue("lock_key", LockKey);
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private async Task ReleaseLockAsync(CancellationToken cancellationToken)
	{
		await using var command = new NpgsqlCommand(
			"""
			SELECT
				pg_advisory_unlock(hashtext(@lock_key));
			""",
			_connection);
		command.Parameters.AddWithValue("lock_key", LockKey);
		await command.ExecuteNonQueryAsync(cancellationToken);
	}
}
