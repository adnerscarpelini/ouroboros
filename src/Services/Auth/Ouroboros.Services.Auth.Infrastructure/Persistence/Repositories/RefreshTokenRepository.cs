using Npgsql;
using Ouroboros.BuildingBlocks.Infrastructure;
using Ouroboros.Services.Auth.Application;
using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Infrastructure;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
	private readonly DbSession _session;

	public RefreshTokenRepository(DbSession session)
	{
		_session = session;
	}

	public void Add(RefreshToken token)
		=> _session.Enqueue(async (connection, transaction, cancellationToken) =>
		{
			await using var command = new NpgsqlCommand(
				"""
				INSERT INTO auth.refresh_tokens (
					external_id,
					created_at,
					updated_at,
					user_id,
					token_hash,
					expires_at,
					revoked_at
				)
				SELECT
					@external_id,
					@created_at,
					@updated_at,
					id,
					@token_hash,
					@expires_at,
					@revoked_at
				FROM
					auth.users
				WHERE
					external_id = @user_external_id;
				""",
				(NpgsqlConnection)connection,
				(NpgsqlTransaction)transaction);

			command.Parameters.AddWithValue("external_id", token.ExternalId);
			command.Parameters.AddWithValue("created_at", token.CreatedAt);
			command.Parameters.AddWithValue("updated_at", (object?)token.UpdatedAt ?? DBNull.Value);
			command.Parameters.AddWithValue("user_external_id", token.User.ExternalId);
			command.Parameters.AddWithValue("token_hash", token.TokenHash);
			command.Parameters.AddWithValue("expires_at", token.ExpiresAt);
			command.Parameters.AddWithValue("revoked_at", (object?)token.RevokedAt ?? DBNull.Value);

			if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
			{
				throw new InvalidOperationException("Usuário do refresh token não encontrado.");
			}
		});

	public void Update(RefreshToken token)
	{
		token.MarkAsUpdated();
		_session.Enqueue(async (connection, transaction, cancellationToken) =>
		{
			await using var command = new NpgsqlCommand(
				"""
				UPDATE auth.refresh_tokens
				SET
					revoked_at = @revoked_at,
					updated_at = @updated_at
				WHERE
					external_id = @external_id;
				""",
				(NpgsqlConnection)connection,
				(NpgsqlTransaction)transaction);
			command.Parameters.AddWithValue("revoked_at", (object?)token.RevokedAt ?? DBNull.Value);
			command.Parameters.AddWithValue("updated_at", (object?)token.UpdatedAt ?? DBNull.Value);
			command.Parameters.AddWithValue("external_id", token.ExternalId);

			if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
			{
				throw new InvalidOperationException("Refresh token não encontrado para atualização.");
			}
		});
	}

	public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken)
	{
		await _session.OpenAsync(cancellationToken);
		await using var command = new NpgsqlCommand(
			"""
			SELECT
				refreshTokens.id,
				refreshTokens.external_id,
				refreshTokens.created_at,
				refreshTokens.updated_at,
				refreshTokens.token_hash,
				refreshTokens.expires_at,
				refreshTokens.revoked_at,
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
				auth.refresh_tokens AS refreshTokens
			INNER JOIN
				auth.users AS users
				ON users.id = refreshTokens.user_id
			WHERE
				refreshTokens.token_hash = @token_hash
			LIMIT 1;
			""",
			(NpgsqlConnection)_session.Connection,
			(NpgsqlTransaction?)_session.Transaction);
			command.Parameters.AddWithValue("token_hash", tokenHash);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);

		if (!await reader.ReadAsync(cancellationToken))
		{
			return null;
		}

		var user = UserRowMapper.Map(reader, "user_");

		return RefreshToken.Rehydrate(
			id: reader.GetInt64(reader.GetOrdinal("id")),
			externalId: reader.GetGuid(reader.GetOrdinal("external_id")),
			createdAt: reader.GetDateTime(reader.GetOrdinal("created_at")),
			updatedAt: Nullable(reader, "updated_at"),
			userId: reader.GetInt64(reader.GetOrdinal("user_id")),
			user: user,
			tokenHash: reader.GetString(reader.GetOrdinal("token_hash")),
			expiresAt: reader.GetDateTime(reader.GetOrdinal("expires_at")),
			revokedAt: Nullable(reader, "revoked_at")
		);
	}

	private static DateTime? Nullable(System.Data.Common.DbDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);

		if (reader.IsDBNull(ordinal))
		{
			return null;
		}

		return reader.GetDateTime(ordinal);
	}
}
