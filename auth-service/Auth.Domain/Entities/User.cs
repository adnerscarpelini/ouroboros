namespace Ouroboros.Auth.Domain.Entities;

using Ouroboros.Auth.Domain.Exceptions;
using Ouroboros.Auth.Domain.Policies;

public sealed class User : Entity
{
    public const int MaxFailedAccessAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public string Login { get; private set; } = null!;

    // Valores de comparacao (spec 2026092508): derivados de Login/Email, nunca informados de fora.
    public string NormalizedLogin { get; private set; } = null!;

    public string FullName { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public string NormalizedEmail { get; private set; } = null!;

    public bool EmailConfirmed { get; private set; }

    public string PasswordHash { get; private set; } = null!;

    public DateTimeOffset PasswordChangedAt { get; private set; }

    public bool Active { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public UserRole Role { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public int AccessFailedCount { get; private set; }

    public DateTimeOffset? LockoutEnd { get; private set; }

    private User()
    {
    }

    public static User Create(
        string login,
        string fullName,
        string email,
        string passwordHash)
    {
        var validLogin = IdentityPolicy.ValidateLogin(login);
        var validEmail = IdentityPolicy.ValidateEmail(email);

        var user = new User
        {
            Login = validLogin,
            NormalizedLogin = IdentityPolicy.Normalize(validLogin),
            FullName = ValidateFullName(fullName),
            Email = validEmail,
            NormalizedEmail = IdentityPolicy.Normalize(validEmail),
            PasswordHash = ValidatePasswordHash(passwordHash),
            EmailConfirmed = false,
            PasswordChangedAt = DateTimeOffset.UtcNow,
            Active = false,
            LastLoginAt = null,
            // Menor privilegio: todo cadastro nasce User; promocao a Admin nunca vem do request de cadastro.
            Role = UserRole.User,
            DeletedAt = null,
            AccessFailedCount = 0,
            LockoutEnd = null,
        };

        return user;
    }

    public static User Rehydrate(
        long id,
        Guid externalId,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt,
        string login,
        string fullName,
        string email,
        bool emailConfirmed,
        string passwordHash,
        DateTimeOffset passwordChangedAt,
        bool active,
        DateTimeOffset? lastLoginAt,
        UserRole role,
        DateTimeOffset? deletedAt,
        int accessFailedCount = 0,
        DateTimeOffset? lockoutEnd = null)
    {
        var user = new User
        {
            Login = login,
            NormalizedLogin = IdentityPolicy.Normalize(login),
            FullName = fullName,
            Email = email,
            NormalizedEmail = IdentityPolicy.Normalize(email),
            EmailConfirmed = emailConfirmed,
            PasswordHash = passwordHash,
            PasswordChangedAt = passwordChangedAt,
            Active = active,
            LastLoginAt = lastLoginAt,
            Role = role,
            DeletedAt = deletedAt,
            AccessFailedCount = accessFailedCount,
            LockoutEnd = lockoutEnd,
        };

        user.RestorePersistence(id, externalId, createdAt, updatedAt);

        return user;
    }

    public void ConfirmEmail()
    {
        EmailConfirmed = true;
        Active = true;
        MarkAsUpdated();
    }

    public bool IsLockedOut(DateTimeOffset now)
    {
        return LockoutEnd > now;
    }

    // Em producao a regra roda atomica no UPDATE de DapperUserRepository.RecordFailedAccessAsync,
    // pra falhas concorrentes nao se perderem. Qualquer mudanca aqui precisa ser replicada la.
    public bool RecordFailedAccess(DateTimeOffset now)
    {
        if (IsLockedOut(now))
        {
            return false;
        }

        LockoutEnd = null;
        AccessFailedCount++;

        if (AccessFailedCount < MaxFailedAccessAttempts)
        {
            return false;
        }

        AccessFailedCount = 0;
        LockoutEnd = now.Add(LockoutDuration);
        return true;
    }

    public void ResetFailedAccess()
    {
        AccessFailedCount = 0;
        LockoutEnd = null;
    }

    public void ChangePassword(string passwordHash)
    {
        PasswordHash = ValidatePasswordHash(passwordHash);
        PasswordChangedAt = DateTimeOffset.UtcNow;
        MarkAsUpdated();
    }

    // Exclusao logica: a linha fica no banco pra auditoria; o login continua reservado e o e-mail fica livre.
    public void Delete()
    {
        if (DeletedAt is not null)
        {
            throw new DomainException("User has already been deleted");
        }

        DeletedAt = DateTimeOffset.UtcNow;
        Active = false;
        MarkAsUpdated();
    }

    private static string ValidateFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("Full name is required");
        }

        return fullName.Trim();
    }

    private static string ValidatePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Password hash is required");
        }

        return passwordHash;
    }
}
