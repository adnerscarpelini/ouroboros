namespace Ouroboros.Auth.Infrastructure.Persistence;

using Dapper;
using Npgsql;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;

public sealed class DapperRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly string _connectionString;

    public DapperRefreshTokenRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task AddAsync(RefreshToken refreshToken)
    {
        const string sql = """
            INSERT INTO auth.refresh_tokens (
                id,
                external_id,
                created_at,
                updated_at,
                user_id,
                token_hash,
                expires_at,
                revoked_at
            )
            SELECT
                nextval('auth.refresh_tokens_id_seq'),
                @ExternalId,
                @CreatedAt,
                @UpdatedAt,
                users.id,
                @TokenHash,
                @ExpiresAt,
                @RevokedAt
            FROM auth.users AS users
            WHERE users.external_id = @UserExternalId;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);

        var affectedRows = await connection.ExecuteAsync(
            sql,
            new
            {
                refreshToken.ExternalId,
                refreshToken.CreatedAt,
                refreshToken.UpdatedAt,
                refreshToken.TokenHash,
                refreshToken.ExpiresAt,
                refreshToken.RevokedAt,
                refreshToken.UserExternalId,
            });

        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"User '{refreshToken.UserExternalId}' not found while adding refresh token.");
        }
    }
}
