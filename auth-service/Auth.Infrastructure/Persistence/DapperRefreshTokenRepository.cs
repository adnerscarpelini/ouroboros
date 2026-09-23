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

    public async Task<RefreshToken?> GetByHashAsync(string tokenHash)
    {
        const string sql = """
            SELECT
                refreshTokens.id,
                refreshTokens.external_id,
                refreshTokens.created_at,
                refreshTokens.updated_at,
                users.external_id AS user_external_id,
                refreshTokens.token_hash,
                refreshTokens.expires_at,
                refreshTokens.revoked_at
            FROM
                auth.refresh_tokens AS refreshTokens
            INNER JOIN
                auth.users AS users
                ON users.id = refreshTokens.user_id
            WHERE
                refreshTokens.token_hash = @TokenHash
            LIMIT 1;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);

        var row = await connection.QuerySingleOrDefaultAsync<RefreshTokenRow>(sql, new { TokenHash = tokenHash });

        if (row is null)
        {
            return null;
        }

        return RefreshToken.Rehydrate(
            row.Id,
            row.ExternalId,
            new DateTimeOffset(row.CreatedAt),
            ToDateTimeOffset(row.UpdatedAt),
            row.UserExternalId,
            row.TokenHash,
            new DateTimeOffset(row.ExpiresAt),
            ToDateTimeOffset(row.RevokedAt));
    }

    public async Task<bool> TryRevokeAsync(RefreshToken refreshToken)
    {
        // "revoked_at IS NULL" garante que so uma requisicao concorrente consegue revogar o mesmo token.
        const string sql = """
            UPDATE auth.refresh_tokens
            SET
                updated_at = @UpdatedAt,
                revoked_at = @RevokedAt
            WHERE
                external_id = @ExternalId
                AND revoked_at IS NULL;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);

        var affectedRows = await connection.ExecuteAsync(
            sql,
            new
            {
                refreshToken.UpdatedAt,
                refreshToken.RevokedAt,
                refreshToken.ExternalId,
            });

        return affectedRows > 0;
    }

    public async Task RevokeAllActiveByUserAsync(Guid userExternalId, DateTimeOffset revokedAt)
    {
        const string sql = """
            UPDATE auth.refresh_tokens AS refreshTokens
            SET
                updated_at = @RevokedAt,
                revoked_at = @RevokedAt
            FROM auth.users AS users
            WHERE
                users.id = refreshTokens.user_id
                AND users.external_id = @UserExternalId
                AND refreshTokens.revoked_at IS NULL
                AND refreshTokens.expires_at > @RevokedAt;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);

        await connection.ExecuteAsync(
            sql,
            new
            {
                RevokedAt = revokedAt,
                UserExternalId = userExternalId,
            });
    }

    private static DateTimeOffset? ToDateTimeOffset(DateTime? value)
    {
        if (value is null)
        {
            return null;
        }

        return new DateTimeOffset(value.Value);
    }

    // Npgsql le timestamptz como DateTime (Kind=Utc); a conversao pra DateTimeOffset e feita no mapeamento.
    private sealed class RefreshTokenRow
    {
        public long Id { get; init; }

        public Guid ExternalId { get; init; }

        public DateTime CreatedAt { get; init; }

        public DateTime? UpdatedAt { get; init; }

        public Guid UserExternalId { get; init; }

        public string TokenHash { get; init; } = null!;

        public DateTime ExpiresAt { get; init; }

        public DateTime? RevokedAt { get; init; }
    }
}
