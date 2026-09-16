using Npgsql;
using Ouroboros.BuildingBlocks.Infrastructure;
using Ouroboros.AuthService.Application;
using Ouroboros.AuthService.Domain;

namespace Ouroboros.AuthService.Infrastructure;

public sealed class UserRepository : IUserRepository
{
	private const string SelectColumns = """
		id,
		external_id,
		created_at,
		updated_at,
		login,
		full_name,
		email,
		email_confirmed,
		password_hash,
		password_changed_at,
		is_active,
		failed_login_attempts,
		locked_until,
		last_login_at
		""";

	private readonly DbSession _session;

	public UserRepository(DbSession session)
	{
		_session = session;
	}

	public void Add(User user)
	{
		_session.Enqueue(async (connection, transaction, cancellationToken) =>
		{
			await using var command = new NpgsqlCommand(
				"""
				INSERT INTO auth.users (
					external_id,
					created_at,
					updated_at,
					login,
					full_name,
					email,
					email_confirmed,
					password_hash,
					password_changed_at,
					is_active,
					failed_login_attempts,
					locked_until,
					last_login_at
				)
				VALUES (
					@external_id,
					@created_at,
					@updated_at,
					@login,
					@full_name,
					@email,
					@email_confirmed,
					@password_hash,
					@password_changed_at,
					@is_active,
					@failed_login_attempts,
					@locked_until,
					@last_login_at
				);
				""",
				(NpgsqlConnection)connection,
				(NpgsqlTransaction)transaction);

			AddUserParameters(command, user);
			await command.ExecuteNonQueryAsync(cancellationToken);
		});
	}

	public void Update(User user)
	{
		user.MarkAsUpdated();
		_session.Enqueue(async (connection, transaction, cancellationToken) =>
		{
			await using var command = new NpgsqlCommand(
				"""
				UPDATE auth.users
				SET
					login = @login,
					full_name = @full_name,
					email = @email,
					email_confirmed = @email_confirmed,
					password_hash = @password_hash,
					password_changed_at = @password_changed_at,
					is_active = @is_active,
					failed_login_attempts = @failed_login_attempts,
					locked_until = @locked_until,
					last_login_at = @last_login_at,
					updated_at = @updated_at
				WHERE
					external_id = @external_id;
				""",
				(NpgsqlConnection)connection,
				(NpgsqlTransaction)transaction);

			AddUserParameters(command, user);
			var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);

			if (affectedRows != 1)
			{
				throw new InvalidOperationException($"Usuário '{user.ExternalId}' não encontrado para atualização.");
			}
		});
	}

	public Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken)
		=> QuerySingleAsync(
			$"""
			SELECT
				{SelectColumns}
			FROM
				auth.users
			WHERE
				login = @value
			LIMIT 1;
			""",
			login,
			cancellationToken);

	public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
		=> QuerySingleAsync(
			$"""
			SELECT
				{SelectColumns}
			FROM
				auth.users
			WHERE
				email = @value
			LIMIT 1;
			""",
			email,
			cancellationToken);

	public Task<bool> ExistsByLoginAsync(string login, CancellationToken cancellationToken)
		=> ExistsAsync(
			"""
			SELECT EXISTS (
				SELECT 1
				FROM
					auth.users
				WHERE
					login = @value
			);
			""",
			login,
			cancellationToken);

	public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken)
		=> ExistsAsync(
			"""
			SELECT EXISTS (
				SELECT 1
				FROM
					auth.users
				WHERE
					email = @value
			);
			""",
			email,
			cancellationToken);

	private async Task<User?> QuerySingleAsync(
		string sql,
		string value,
		CancellationToken cancellationToken)
	{
		await _session.OpenAsync(cancellationToken);
		await using var command = new NpgsqlCommand(
			sql,
			(NpgsqlConnection)_session.Connection,
			(NpgsqlTransaction?)_session.Transaction);
		command.Parameters.AddWithValue("value", value);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		if (!await reader.ReadAsync(cancellationToken))
		{
			return null;
		}

		return UserRowMapper.Map(reader);
	}

	private async Task<bool> ExistsAsync(
		string sql,
		string value,
		CancellationToken cancellationToken)
	{
		await _session.OpenAsync(cancellationToken);
		await using var command = new NpgsqlCommand(
			sql,
			(NpgsqlConnection)_session.Connection,
			(NpgsqlTransaction?)_session.Transaction);
		command.Parameters.AddWithValue("value", value);

		return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
	}

	private static void AddUserParameters(NpgsqlCommand command, User user)
	{
		command.Parameters.AddWithValue("external_id", user.ExternalId);
		command.Parameters.AddWithValue("created_at", user.CreatedAt);
		command.Parameters.AddWithValue("updated_at", (object?)user.UpdatedAt ?? DBNull.Value);
		command.Parameters.AddWithValue("login", user.Login);
		command.Parameters.AddWithValue("full_name", user.FullName);
		command.Parameters.AddWithValue("email", user.Email);
		command.Parameters.AddWithValue("email_confirmed", user.EmailConfirmed);
		command.Parameters.AddWithValue("password_hash", user.PasswordHash);
		command.Parameters.AddWithValue("password_changed_at", user.PasswordChangedAt);
		command.Parameters.AddWithValue("is_active", user.IsActive);
		command.Parameters.AddWithValue("failed_login_attempts", user.FailedLoginAttempts);
		command.Parameters.AddWithValue("locked_until", (object?)user.LockedUntil ?? DBNull.Value);
		command.Parameters.AddWithValue("last_login_at", (object?)user.LastLoginAt ?? DBNull.Value);
	}
}
