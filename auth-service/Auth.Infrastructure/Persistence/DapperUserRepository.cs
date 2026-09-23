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
                last_login_at
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
                @LastLoginAt
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
            });
    }

    public async Task<bool> ExistsByLoginOrEmailAsync(string login, string email)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM auth.users AS users
                WHERE users.login = @Login
                OR users.email = @Email
            );
            """;

        await using var connection = new NpgsqlConnection(_connectionString);

        return await connection.ExecuteScalarAsync<bool>(sql, new { Login = login, Email = email });
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
                users.last_login_at
            FROM auth.users AS users
            WHERE users.external_id = @ExternalId;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);

        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(sql, new { ExternalId = externalId });

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
                users.last_login_at
            FROM auth.users AS users
            WHERE users.login = @Login;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);

        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(sql, new { Login = login });

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
                last_login_at = @LastLoginAt
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
                user.ExternalId,
            });
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
            ToDateTimeOffset(row.LastLoginAt));
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
    }
}
