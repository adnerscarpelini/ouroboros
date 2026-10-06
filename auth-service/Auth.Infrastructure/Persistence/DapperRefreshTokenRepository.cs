namespace Ouroboros.Auth.Infrastructure.Persistence;

using Dapper;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;

// Hints de leitura (skill ouroboros-dba): SELECT de decisao usa WITH (READPAST), que so devolve linha commitada e pula a
// linha travada por outra transacao (para quem le, ela some). SELECT informativo usa WITH (NOLOCK). UPDATE e DELETE nao levam hint.
public sealed class DapperRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly DbSession _session;

    public DapperRefreshTokenRepository(DbSession session)
    {
        _session = session;
    }

    public async Task AddAsync(RefreshToken refreshToken)
    {
        const string sql = """
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

        var affectedRows = await _session.ExecuteAsync(
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
            SELECT TOP 1
                refreshTokens.id,
                refreshTokens.external_id,
                refreshTokens.created_at,
                refreshTokens.updated_at,
                users.external_id AS user_external_id,
                refreshTokens.token_hash,
                refreshTokens.expires_at,
                refreshTokens.revoked_at
            FROM
                auth.refresh_tokens AS refreshTokens WITH (READPAST)
            INNER JOIN
                auth.users AS users WITH (READPAST)
                ON users.id = refreshTokens.user_id
            WHERE
                refreshTokens.token_hash = @TokenHash;
            """;

        var row = await _session.QuerySingleOrDefaultAsync<RefreshTokenRow>(sql, new { TokenHash = tokenHash });

        if (row is null)
        {
            return null;
        }

        return RefreshToken.Rehydrate(
            row.Id,
            row.ExternalId,
            row.CreatedAt,
            row.UpdatedAt,
            row.UserExternalId,
            row.TokenHash,
            row.ExpiresAt,
            row.RevokedAt);
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

        var affectedRows = await _session.ExecuteAsync(
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
            UPDATE refreshTokens
            SET
                updated_at = @RevokedAt,
                revoked_at = @RevokedAt
            FROM auth.refresh_tokens AS refreshTokens
            INNER JOIN auth.users AS users
                ON users.id = refreshTokens.user_id
            WHERE
                users.external_id = @UserExternalId
                AND refreshTokens.revoked_at IS NULL
                AND refreshTokens.expires_at > @RevokedAt;
            """;

        await _session.ExecuteAsync(
            sql,
            new
            {
                RevokedAt = revokedAt,
                UserExternalId = userExternalId,
            });
    }


    private sealed class RefreshTokenRow
    {
        public long Id { get; init; }

        public Guid ExternalId { get; init; }

        public DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset? UpdatedAt { get; init; }

        public Guid UserExternalId { get; init; }

        public string TokenHash { get; init; } = null!;

        public DateTimeOffset ExpiresAt { get; init; }

        public DateTimeOffset? RevokedAt { get; init; }
    }
}
