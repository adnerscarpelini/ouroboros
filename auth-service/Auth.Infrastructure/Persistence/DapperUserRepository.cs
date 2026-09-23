namespace Ouroboros.Auth.Infrastructure.Persistence;

using Dapper;
using Npgsql;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;

public sealed class DapperUserRepository : IUserRepository
{
    private readonly string _connectionString;

    public DapperUserRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task AddAsync(User user)
    {
        const string sql = """
            INSERT INTO auth.users (
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
                active,
                last_login_at,
                role,
                deleted_at
            )
            VALUES (
                nextval('auth.users_id_seq'),
                @ExternalId,
                @CreatedAt,
                @UpdatedAt,
                @Login,
                @FullName,
                @Email,
                @EmailConfirmed,
                @PasswordHash,
                @PasswordChangedAt,
                @Active,
                @LastLoginAt,
                @Role,
                @DeletedAt
            );
            """;

        await using var connection = new NpgsqlConnection(_connectionString);

        await connection.ExecuteAsync(
            sql,
            new
            {
                user.ExternalId,
                user.CreatedAt,
                user.UpdatedAt,
                user.Login,
                user.FullName,
                user.Email,
                user.EmailConfirmed,
                user.PasswordHash,
                user.PasswordChangedAt,
                user.Active,
                user.LastLoginAt,
                Role = user.Role.ToString(),
                user.DeletedAt,
            });
    }

    public async Task<User?> GetByExternalIdAsync(Guid externalId)
    {
        const string sql = """
            SELECT
                users.id,
                users.external_id,
                users.created_at,
                users.updated_at,
                users.login,
                users.full_name,
                users.email,
                users.email_confirmed,
                users.password_hash,
                users.password_changed_at,
                users.active,
                users.last_login_at,
                users.role,
                users.deleted_at
            FROM auth.users AS users
            WHERE
                users.external_id = @ExternalId
                AND users.deleted_at IS NULL;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);

        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(sql, new { ExternalId = externalId });

        return MapToUser(row);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        const string sql = """
            SELECT
                users.id,
                users.external_id,
                users.created_at,
                users.updated_at,
                users.login,
                users.full_name,
                users.email,
                users.email_confirmed,
                users.password_hash,
                users.password_changed_at,
                users.active,
                users.last_login_at,
                users.role,
                users.deleted_at
            FROM auth.users AS users
            WHERE
                users.email = @Email
                AND users.deleted_at IS NULL;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);

        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(sql, new { Email = email });

        return MapToUser(row);
    }

    public async Task<User?> GetByLoginAsync(string login)
    {
        const string sql = """
            SELECT
                users.id,
                users.external_id,
                users.created_at,
                users.updated_at,
                users.login,
                users.full_name,
                users.email,
                users.email_confirmed,
                users.password_hash,
                users.password_changed_at,
                users.active,
                users.last_login_at,
                users.role,
                users.deleted_at
            FROM auth.users AS users
            WHERE
                users.login = @Login
                AND users.deleted_at IS NULL;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);

        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(sql, new { Login = login });

        return MapToUser(row);
    }

    public async Task<User?> GetByLoginOrEmailAsync(string loginOrEmail)
    {
        // Se o valor bater com o login de um usuario e o e-mail de outro, o login tem prioridade.
        const string sql = """
            SELECT
                users.id,
                users.external_id,
                users.created_at,
                users.updated_at,
                users.login,
                users.full_name,
                users.email,
                users.email_confirmed,
                users.password_hash,
                users.password_changed_at,
                users.active,
                users.last_login_at,
                users.role,
                users.deleted_at
            FROM auth.users AS users
            WHERE
                (users.login = @LoginOrEmail OR users.email = @LoginOrEmail)
                AND users.deleted_at IS NULL
            ORDER BY (users.login = @LoginOrEmail) DESC
            LIMIT 1;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);

        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(sql, new { LoginOrEmail = loginOrEmail });

        return MapToUser(row);
    }

    public async Task UpdateAsync(User user)
    {
        const string sql = """
            UPDATE auth.users
            SET
                updated_at = @UpdatedAt,
                full_name = @FullName,
                email = @Email,
                email_confirmed = @EmailConfirmed,
                password_hash = @PasswordHash,
                password_changed_at = @PasswordChangedAt,
                active = @Active,
                last_login_at = @LastLoginAt,
                role = @Role,
                deleted_at = @DeletedAt
            WHERE
                external_id = @ExternalId;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);

        await connection.ExecuteAsync(
            sql,
            new
            {
                user.UpdatedAt,
                user.FullName,
                user.Email,
                user.EmailConfirmed,
                user.PasswordHash,
                user.PasswordChangedAt,
                user.Active,
                user.LastLoginAt,
                Role = user.Role.ToString(),
                user.DeletedAt,
                user.ExternalId,
            });
    }

    public async Task RemoveAsync(Guid externalId)
    {
        // Tokens e refresh tokens do usuario saem junto via ON DELETE CASCADE.
        // Conta excluida (logicamente) nunca e apagada: a linha fica pra auditoria e pra reservar o login.
        const string sql = """
            DELETE FROM auth.users
            WHERE
                external_id = @ExternalId
                AND deleted_at IS NULL;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);

        await connection.ExecuteAsync(sql, new { ExternalId = externalId });
    }

    public async Task<bool> ExistsDeletedByLoginAsync(string login)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM auth.users AS users
                WHERE
                    users.login = @Login
                    AND users.deleted_at IS NOT NULL
            );
            """;

        await using var connection = new NpgsqlConnection(_connectionString);

        return await connection.ExecuteScalarAsync<bool>(sql, new { Login = login });
    }

    public async Task<int> CountActiveAdminsAsync()
    {
        const string sql = """
            SELECT COUNT(*)
            FROM auth.users AS users
            WHERE
                users.role = @Role
                AND users.active
                AND users.deleted_at IS NULL;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);

        return await connection.ExecuteScalarAsync<int>(sql, new { Role = nameof(UserRole.Admin) });
    }

    private static User? MapToUser(UserRow? row)
    {
        if (row is null)
        {
            return null;
        }

        return User.Rehydrate(
            row.Id,
            row.ExternalId,
            new DateTimeOffset(row.CreatedAt),
            ToDateTimeOffset(row.UpdatedAt),
            row.Login,
            row.FullName,
            row.Email,
            row.EmailConfirmed,
            row.PasswordHash,
            new DateTimeOffset(row.PasswordChangedAt),
            row.Active,
            ToDateTimeOffset(row.LastLoginAt),
            Enum.Parse<UserRole>(row.Role),
            ToDateTimeOffset(row.DeletedAt));
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
    private sealed class UserRow
    {
        public long Id { get; init; }

        public Guid ExternalId { get; init; }

        public DateTime CreatedAt { get; init; }

        public DateTime? UpdatedAt { get; init; }

        public string Login { get; init; } = null!;

        public string FullName { get; init; } = null!;

        public string Email { get; init; } = null!;

        public bool EmailConfirmed { get; init; }

        public string PasswordHash { get; init; } = null!;

        public DateTime PasswordChangedAt { get; init; }

        public bool Active { get; init; }

        public DateTime? LastLoginAt { get; init; }

        // Gravado como texto (nome do enum) pra casar com o CHECK da coluna e ficar legivel em SQL.
        public string Role { get; init; } = null!;

        public DateTime? DeletedAt { get; init; }
    }
}
