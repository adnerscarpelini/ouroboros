namespace Ouroboros.Auth.Infrastructure.Persistence;

using Dapper;
using Npgsql;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;

public sealed class DapperTokenRepository : ITokenRepository
{
    private readonly string _connectionString;

    public DapperTokenRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task AddAsync(Token token)
    {
        const string sql = """
            INSERT INTO auth.tokens (
                id,
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
                nextval('auth.tokens_id_seq'),
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

        await using var connection = new NpgsqlConnection(_connectionString);

        var affectedRows = await connection.ExecuteAsync(
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
            SELECT
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
                auth.tokens AS tokens
            INNER JOIN
                auth.users AS users
                ON users.id = tokens.user_id
            WHERE
                tokens.token_hash = @TokenHash
                AND tokens.type = @Type
            LIMIT 1;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);

        var row = await connection.QuerySingleOrDefaultAsync<TokenRow>(
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
            new DateTimeOffset(row.CreatedAt),
            ToDateTimeOffset(row.UpdatedAt),
            row.UserExternalId,
            Enum.Parse<TokenType>(row.Type),
            row.TokenHash,
            new DateTimeOffset(row.ExpiresAt),
            ToDateTimeOffset(row.UsedAt));
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

        await using var connection = new NpgsqlConnection(_connectionString);

        await connection.ExecuteAsync(
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
        const string sql = """
            UPDATE auth.tokens
            SET
                updated_at = @UpdatedAt,
                used_at = @UsedAt
            WHERE
                external_id = @ExternalId
                AND used_at IS NULL;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);

        var affectedRows = await connection.ExecuteAsync(
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
            UPDATE auth.tokens AS tokens
            SET
                updated_at = @InvalidatedAt,
                expires_at = @InvalidatedAt
            FROM auth.users AS users
            WHERE
                users.id = tokens.user_id
                AND users.external_id = @UserExternalId
                AND tokens.type = @Type
                AND tokens.used_at IS NULL
                AND tokens.expires_at > @InvalidatedAt;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);

        await connection.ExecuteAsync(
            sql,
            new
            {
                InvalidatedAt = invalidatedAt,
                UserExternalId = userExternalId,
                Type = type.ToString(),
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
    private sealed class TokenRow
    {
        public long Id { get; init; }

        public Guid ExternalId { get; init; }

        public DateTime CreatedAt { get; init; }

        public DateTime? UpdatedAt { get; init; }

        public Guid UserExternalId { get; init; }

        public string Type { get; init; } = null!;

        public string TokenHash { get; init; } = null!;

        public DateTime ExpiresAt { get; init; }

        public DateTime? UsedAt { get; init; }
    }
}
