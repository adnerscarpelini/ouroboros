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
}
