namespace Ouroboros.Auth.Infrastructure.Persistence;

using Dapper;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;

// Hints de leitura (skill ouroboros-dba): SELECT de decisao usa WITH (READPAST), que so devolve linha commitada e pula a
// linha travada por outra transacao (para quem le, ela some). SELECT informativo usa WITH (NOLOCK). UPDATE e DELETE nao levam hint.
public sealed class DapperTokenRepository : ITokenRepository
{
    private readonly DbSession _session;

    public DapperTokenRepository(DbSession session)
    {
        _session = session;
    }

    public async Task AddAsync(Token token)
    {
        const string sql = """
            INSERT INTO auth.tokens (
                external_id,
                created_at,
                updated_at,
                user_id,
                type,
                token_hash,
                expires_at,
                used_at
            )
            SELECT
                @ExternalId,
                @CreatedAt,
                @UpdatedAt,
                users.id,
                @Type,
                @TokenHash,
                @ExpiresAt,
                @UsedAt
            FROM auth.users AS users
            WHERE users.external_id = @UserExternalId;
            """;

        var affectedRows = await _session.ExecuteAsync(
            sql,
            new
            {
                token.ExternalId,
                token.CreatedAt,
                token.UpdatedAt,
                Type = token.Type.ToString(),
                token.TokenHash,
                token.ExpiresAt,
                token.UsedAt,
                token.UserExternalId,
            });

        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"User '{token.UserExternalId}' not found while adding token.");
        }
    }

    public async Task<Token?> GetByHashAsync(string tokenHash, TokenType type)
    {
        const string sql = """
            SELECT TOP 1
                tokens.id,
                tokens.external_id,
                tokens.created_at,
                tokens.updated_at,
                users.external_id AS user_external_id,
                tokens.type,
                tokens.token_hash,
                tokens.expires_at,
                tokens.used_at
            FROM
                auth.tokens AS tokens WITH (READPAST)
            INNER JOIN
                auth.users AS users WITH (READPAST)
                ON users.id = tokens.user_id
            WHERE
                tokens.token_hash = @TokenHash
                AND tokens.type = @Type;
            """;

        var row = await _session.QuerySingleOrDefaultAsync<TokenRow>(
            sql,
            new
            {
                TokenHash = tokenHash,
                Type = type.ToString(),
            });

        if (row is null)
        {
            return null;
        }

        return Token.Rehydrate(
            row.Id,
            row.ExternalId,
            row.CreatedAt,
            row.UpdatedAt,
            row.UserExternalId,
            Enum.Parse<TokenType>(row.Type),
            row.TokenHash,
            row.ExpiresAt,
            row.UsedAt);
    }

    public async Task<bool> ExistsPendingByUserAsync(
        Guid userExternalId,
        TokenType type,
        DateTimeOffset now)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM
                    auth.tokens AS tokens WITH (READPAST)
                INNER JOIN
                    auth.users AS users WITH (READPAST)
                    ON users.id = tokens.user_id
                WHERE
                    users.external_id = @UserExternalId
                    AND tokens.type = @Type
                    AND tokens.used_at IS NULL
                    AND tokens.expires_at > @Now
            ) THEN 1 ELSE 0 END;
            """;

        return await _session.ExecuteScalarAsync<bool>(
            sql,
            new
            {
                UserExternalId = userExternalId,
                Type = type.ToString(),
                Now = now,
            });
    }

    public async Task UpdateAsync(Token token)
    {
        const string sql = """
            UPDATE auth.tokens
            SET
                updated_at = @UpdatedAt,
                expires_at = @ExpiresAt,
                used_at = @UsedAt
            WHERE
                external_id = @ExternalId;
            """;

        await _session.ExecuteAsync(
            sql,
            new
            {
                token.UpdatedAt,
                token.ExpiresAt,
                token.UsedAt,
                token.ExternalId,
            });
    }

    public async Task<bool> TryMarkAsUsedAsync(Token token)
    {
        // "used_at IS NULL" garante que so uma requisicao concorrente consegue usar o mesmo token.
        // "expires_at > @UsedAt" confere o prazo no proprio banco: um token invalidado (conta excluida) ou vencido
        // depois da leitura nao e consumido.
        const string sql = """
            UPDATE auth.tokens
            SET
                updated_at = @UpdatedAt,
                used_at = @UsedAt
            WHERE
                external_id = @ExternalId
                AND used_at IS NULL
                AND expires_at > @UsedAt;
            """;

        var affectedRows = await _session.ExecuteAsync(
            sql,
            new
            {
                token.UpdatedAt,
                token.UsedAt,
                token.ExternalId,
            });

        return affectedRows == 1;
    }

    public async Task InvalidatePendingByUserAsync(
        Guid userExternalId,
        TokenType type,
        DateTimeOffset invalidatedAt)
    {
        // Nao existe coluna de revogacao em auth.tokens: invalidar = antecipar a expiracao pro instante atual.
        const string sql = """
            UPDATE tokens
            SET
                updated_at = @InvalidatedAt,
                expires_at = @InvalidatedAt
            FROM auth.tokens AS tokens
            INNER JOIN auth.users AS users
                ON users.id = tokens.user_id
            WHERE
                users.external_id = @UserExternalId
                AND tokens.type = @Type
                AND tokens.used_at IS NULL
                AND tokens.expires_at > @InvalidatedAt;
            """;

        await _session.ExecuteAsync(
            sql,
            new
            {
                InvalidatedAt = invalidatedAt,
                UserExternalId = userExternalId,
                Type = type.ToString(),
            });
    }


    private sealed class TokenRow
    {
        public long Id { get; init; }

        public Guid ExternalId { get; init; }

        public DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset? UpdatedAt { get; init; }

        public Guid UserExternalId { get; init; }

        public string Type { get; init; } = null!;

        public string TokenHash { get; init; } = null!;

        public DateTimeOffset ExpiresAt { get; init; }

        public DateTimeOffset? UsedAt { get; init; }
    }
}
