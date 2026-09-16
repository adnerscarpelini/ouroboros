using System.Data.Common;
using Npgsql;
using Ouroboros.BuildingBlocks.Application;
using Ouroboros.BuildingBlocks.Domain;
using Ouroboros.BuildingBlocks.Infrastructure;
using Ouroboros.AuthService.Application;
using Ouroboros.AuthService.Domain;

namespace Ouroboros.AuthService.Infrastructure;

public sealed class TokenRepository : ITokenRepository
{
	private readonly DbSession _session;

	public TokenRepository(DbSession session)
	{
		_session = session;
	}

	public void Add(Token token) => _session.Enqueue((connection, transaction, cancellationToken) => ExecuteAsync(
		connection, transaction, cancellationToken,
		"""
		INSERT INTO auth.tokens (
			external_id,
			created_at,
			updated_at,
			token_type_id,
			user_id,
			notification_request_id,
			token_hash,
			expires_at,
			validated,
			validated_at
		)
		SELECT
			@external_id,
			@created_at,
			@updated_at,
			tokenTypes.id,
			users.id,
			@notification_request_id,
			@token_hash,
			@expires_at,
			@validated,
			@validated_at
		FROM
			auth.token_types AS tokenTypes
		CROSS JOIN
			auth.users AS users
		WHERE
			tokenTypes.external_id = @token_type_external_id
			AND users.external_id = @user_external_id;
		""", command =>
		{
			command.Parameters.AddWithValue("external_id", token.ExternalId);
			command.Parameters.AddWithValue("created_at", token.CreatedAt);
			command.Parameters.AddWithValue("updated_at", (object?)token.UpdatedAt ?? DBNull.Value);
			command.Parameters.AddWithValue("token_type_external_id", token.TokenType.ExternalId);
			command.Parameters.AddWithValue("user_external_id", token.User.ExternalId);
			command.Parameters.AddWithValue("notification_request_id", token.NotificationRequestId);
			command.Parameters.AddWithValue("token_hash", token.TokenHash);
			command.Parameters.AddWithValue("expires_at", token.ExpiresAt);
			command.Parameters.AddWithValue("validated", token.Validated);
			command.Parameters.AddWithValue("validated_at", (object?)token.ValidatedAt ?? DBNull.Value);
		}));

	public void Update(Token token)
	{
		token.MarkAsUpdated();
		_session.Enqueue((connection, transaction, cancellationToken) => ExecuteAsync(
			connection, transaction, cancellationToken,
			"""
			UPDATE auth.tokens
			SET
				validated = @validated,
				validated_at = @validated_at,
				updated_at = @updated_at
			WHERE
				external_id = @external_id;
			""",
			command =>
			{
				command.Parameters.AddWithValue("validated", token.Validated);
				command.Parameters.AddWithValue("validated_at", (object?)token.ValidatedAt ?? DBNull.Value);
				command.Parameters.AddWithValue("updated_at", (object?)token.UpdatedAt ?? DBNull.Value);
				command.Parameters.AddWithValue("external_id", token.ExternalId);
			}));
	}

	public Task<Token?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken)
		=> QueryAsync(
			"""
			WHERE
				tokens.token_hash = @value
			""",
			command =>
			{
				command.Parameters.AddWithValue("value", tokenHash);
			},
			cancellationToken);

	public async Task<IReadOnlyCollection<Token>> GetPendingByUserAndTypeAsync(User user, string tokenTypeName, CancellationToken cancellationToken)
	{
		var result = new List<Token>();
		await QueryManyAsync(
			"""
			WHERE
				users.external_id = @user_external_id
				AND tokenTypes.name = @token_type_name
				AND NOT tokens.validated
			""",
			command =>
			{
				command.Parameters.AddWithValue("user_external_id", user.ExternalId);
				command.Parameters.AddWithValue("token_type_name", tokenTypeName);
			},
			result, cancellationToken);
		return result;
	}

	private async Task<Token?> QueryAsync(string predicate, Action<NpgsqlCommand> parameters, CancellationToken cancellationToken)
	{
		var result = new List<Token>();
		await QueryManyAsync(predicate, parameters, result, cancellationToken);
		return result.FirstOrDefault();
	}

	private async Task QueryManyAsync(string predicate, Action<NpgsqlCommand> parameters, List<Token>? result, CancellationToken cancellationToken)
	{
		await _session.OpenAsync(cancellationToken);
		await using var command = new NpgsqlCommand(
			TokenSql + "\n" + predicate + ";",
			(NpgsqlConnection)_session.Connection,
			(NpgsqlTransaction?)_session.Transaction);
		parameters(command);
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			var token = TokenMapper.Map(reader);
			result?.Add(token);
		}
	}
	private static readonly string TokenSql = """
		SELECT
			tokens.id,
			tokens.external_id,
			tokens.created_at,
			tokens.updated_at,
			tokens.notification_request_id,
			tokens.token_hash,
			tokens.expires_at,
			tokens.validated,
			tokens.validated_at,
			tokenTypes.id AS token_type_id,
			tokenTypes.external_id AS token_type_external_id,
			tokenTypes.created_at AS token_type_created_at,
			tokenTypes.updated_at AS token_type_updated_at,
			tokenTypes.name AS token_type_name,
			users.id AS user_id,
			users.external_id AS user_external_id,
			users.created_at AS user_created_at,
			users.updated_at AS user_updated_at,
			users.login AS user_login,
			users.full_name AS user_full_name,
			users.email AS user_email,
			users.email_confirmed AS user_email_confirmed,
			users.password_hash AS user_password_hash,
			users.password_changed_at AS user_password_changed_at,
			users.is_active AS user_is_active,
			users.failed_login_attempts AS user_failed_login_attempts,
			users.locked_until AS user_locked_until,
			users.last_login_at AS user_last_login_at
		FROM
			auth.tokens AS tokens
		INNER JOIN
			auth.token_types AS tokenTypes
			ON tokenTypes.id = tokens.token_type_id
		INNER JOIN
			auth.users AS users
			ON users.id = tokens.user_id
		""";

	private static async Task ExecuteAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken, string sql, Action<NpgsqlCommand> parameters)
	{
		await using var command = new NpgsqlCommand(sql, (NpgsqlConnection)connection, (NpgsqlTransaction)transaction);

		parameters(command);

		if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
		{
			throw new InvalidOperationException("A operação de token não afetou exatamente um registro.");
		}
	}
}

internal static class TokenMapper
{
	public static Token Map(DbDataReader reader)
	{
		var tokenType = TokenType.Rehydrate(
			id: reader.GetInt64(reader.GetOrdinal("token_type_id")),
			externalId: reader.GetGuid(reader.GetOrdinal("token_type_external_id")),
			createdAt: reader.GetDateTime(reader.GetOrdinal("token_type_created_at")),
			updatedAt: Nullable(reader, "token_type_updated_at"),
			name: reader.GetString(reader.GetOrdinal("token_type_name"))
		);

		var user = UserRowMapper.Map(reader, "user_");

		return Token.Rehydrate(
			id: reader.GetInt64(reader.GetOrdinal("id")),
			externalId: reader.GetGuid(reader.GetOrdinal("external_id")),
			createdAt: reader.GetDateTime(reader.GetOrdinal("created_at")),
			updatedAt: Nullable(reader, "updated_at"),
			tokenTypeId: reader.GetInt64(reader.GetOrdinal("token_type_id")),
			userId: reader.GetInt64(reader.GetOrdinal("user_id")),
			tokenType: tokenType,
			user: user,
			notificationRequestId: reader.GetGuid(reader.GetOrdinal("notification_request_id")),
			tokenHash: reader.GetString(reader.GetOrdinal("token_hash")),
			expiresAt: reader.GetDateTime(reader.GetOrdinal("expires_at")),
			validated: reader.GetBoolean(reader.GetOrdinal("validated")),
			validatedAt: Nullable(reader, "validated_at")
		);
	}

	private static DateTime? Nullable(DbDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);

		if (reader.IsDBNull(ordinal))
		{
			return null;
		}

		return reader.GetDateTime(ordinal);
	}
}
