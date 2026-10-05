namespace Ouroboros.Auth.Infrastructure.Persistence;

using Dapper;
using Microsoft.Extensions.Logging;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;

public sealed class DapperUserRepository : IUserRepository
{
    private readonly DbSession _session;
    private readonly ILogger<DapperUserRepository> _logger;

    public DapperUserRepository(
        DbSession session,
        ILogger<DapperUserRepository> logger)
    {
        _session = session;
        _logger = logger;
    }

    public async Task AddAsync(User user)
    {
        const string sql = """
            INSERT INTO auth.users (
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
                deleted_at,
                access_failed_count,
                lockout_end
            )
            VALUES (
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
                @DeletedAt,
                @AccessFailedCount,
                @LockoutEnd
            );
            """;

        await _session.ExecuteAsync(
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
                user.AccessFailedCount,
                user.LockoutEnd,
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
                users.deleted_at,
                users.access_failed_count,
                users.lockout_end
            FROM auth.users AS users
            WHERE
                users.external_id = @ExternalId
                AND users.deleted_at IS NULL;
            """;

        var row = await _session.QuerySingleOrDefaultAsync<UserRow>(sql, new { ExternalId = externalId });

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
                users.deleted_at,
                users.access_failed_count,
                users.lockout_end
            FROM auth.users AS users
            WHERE
                users.email = @Email
                AND users.deleted_at IS NULL;
            """;

        var row = await _session.QuerySingleOrDefaultAsync<UserRow>(sql, new { Email = email });

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
                users.deleted_at,
                users.access_failed_count,
                users.lockout_end
            FROM auth.users AS users
            WHERE
                users.login = @Login
                AND users.deleted_at IS NULL;
            """;

        var row = await _session.QuerySingleOrDefaultAsync<UserRow>(sql, new { Login = login });

        return MapToUser(row);
    }

    public async Task<User?> GetByLoginOrEmailAsync(string loginOrEmail)
    {
        // Se o valor bater com o login de um usuario e o e-mail de outro, o login tem prioridade.
        const string sql = """
            SELECT TOP 1
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
                users.deleted_at,
                users.access_failed_count,
                users.lockout_end
            FROM auth.users AS users
            WHERE
                (users.login = @LoginOrEmail OR users.email = @LoginOrEmail)
                AND users.deleted_at IS NULL
            ORDER BY CASE WHEN users.login = @LoginOrEmail THEN 0 ELSE 1 END;
            """;

        var row = await _session.QuerySingleOrDefaultAsync<UserRow>(sql, new { LoginOrEmail = loginOrEmail });

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

        await _session.ExecuteAsync(
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

    // Mesma regra de User.RecordFailedAccess, feita no proprio UPDATE pra ser atomica entre requisicoes e replicas.
    // Qualquer mudanca aqui precisa ser replicada na entidade.
    public async Task<bool> RecordFailedAccessAsync(
        Guid externalId,
        DateTimeOffset now)
    {
        const string sql = """
            UPDATE auth.users
            SET
                access_failed_count = CASE
                    WHEN access_failed_count + 1 >= @MaxAttempts THEN 0
                    ELSE access_failed_count + 1
                END,
                lockout_end = CASE
                    WHEN access_failed_count + 1 >= @MaxAttempts THEN @LockoutEnd
                    ELSE NULL
                END,
                updated_at = @Now
            OUTPUT inserted.lockout_end
            WHERE
                external_id = @ExternalId
                AND deleted_at IS NULL
                AND (lockout_end IS NULL OR lockout_end <= @Now);
            """;

        var lockoutEnd = await _session.QuerySingleOrDefaultAsync<DateTimeOffset?>(
            sql,
            new
            {
                ExternalId = externalId,
                Now = now,
                MaxAttempts = User.MaxFailedAccessAttempts,
                LockoutEnd = now.Add(User.LockoutDuration),
            });

        if (lockoutEnd is null)
        {
            return false;
        }

        _logger.LogWarning("User {ExternalId} locked until {LockoutEnd}", externalId, lockoutEnd);
        return true;
    }

    public async Task<bool> TryResetFailedAccessAsync(
        Guid externalId,
        DateTimeOffset now)
    {
        const string sql = """
            UPDATE auth.users
            SET
                access_failed_count = 0,
                lockout_end = NULL,
                updated_at = @Now
            WHERE
                external_id = @ExternalId
                AND deleted_at IS NULL
                AND (lockout_end IS NULL OR lockout_end <= @Now);
            """;

        return await _session.ExecuteAsync(sql, new { ExternalId = externalId, Now = now }) == 1;
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

        await _session.ExecuteAsync(sql, new { ExternalId = externalId });
    }

    public async Task<bool> ExistsDeletedByLoginAsync(string login)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM auth.users AS users
                WHERE
                    users.login = @Login
                    AND users.deleted_at IS NOT NULL
            ) THEN 1 ELSE 0 END;
            """;

        return await _session.ExecuteScalarAsync<bool>(sql, new { Login = login });
    }

    public async Task<int> CountActiveAdminsAsync()
    {
        const string sql = """
            SELECT COUNT(*)
            FROM auth.users AS users
            WHERE
                users.role = @Role
                AND users.active = 1
                AND users.deleted_at IS NULL;
            """;

        return await _session.ExecuteScalarAsync<int>(sql, new { Role = nameof(UserRole.Admin) });
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
            row.CreatedAt,
            row.UpdatedAt,
            row.Login,
            row.FullName,
            row.Email,
            row.EmailConfirmed,
            row.PasswordHash,
            row.PasswordChangedAt,
            row.Active,
            row.LastLoginAt,
            Enum.Parse<UserRole>(row.Role),
            row.DeletedAt,
            row.AccessFailedCount,
            row.LockoutEnd);
    }


    private sealed class UserRow
    {
        public long Id { get; init; }

        public Guid ExternalId { get; init; }

        public DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset? UpdatedAt { get; init; }

        public string Login { get; init; } = null!;

        public string FullName { get; init; } = null!;

        public string Email { get; init; } = null!;

        public bool EmailConfirmed { get; init; }

        public string PasswordHash { get; init; } = null!;

        public DateTimeOffset PasswordChangedAt { get; init; }

        public bool Active { get; init; }

        public DateTimeOffset? LastLoginAt { get; init; }

        // Gravado como texto (nome do enum) pra casar com o CHECK da coluna e ficar legivel em SQL.
        public string Role { get; init; } = null!;

        public DateTimeOffset? DeletedAt { get; init; }

        public int AccessFailedCount { get; init; }

        public DateTimeOffset? LockoutEnd { get; init; }
    }
}
