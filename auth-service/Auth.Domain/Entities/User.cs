namespace Ouroboros.Auth.Domain.Entities;

using Ouroboros.Auth.Domain.Exceptions;

public sealed class User : Entity
{
    public string Login { get; private set; } = null!;

    public string FullName { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public bool EmailConfirmed { get; private set; }

    public string PasswordHash { get; private set; } = null!;

    public DateTimeOffset PasswordChangedAt { get; private set; }

    public bool Active { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    private User()
    {
    }

    public static User Create(
        string login,
        string fullName,
        string email,
        string passwordHash)
    {
        var user = new User
        {
            Login = ValidateLogin(login),
            FullName = ValidateFullName(fullName),
            Email = ValidateEmail(email),
            PasswordHash = ValidatePasswordHash(passwordHash),
            EmailConfirmed = false,
            PasswordChangedAt = DateTimeOffset.UtcNow,
            Active = false,
            LastLoginAt = null,
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
        DateTimeOffset? lastLoginAt)
    {
        var user = new User
        {
            Login = login,
            FullName = fullName,
            Email = email,
            EmailConfirmed = emailConfirmed,
            PasswordHash = passwordHash,
            PasswordChangedAt = passwordChangedAt,
            Active = active,
            LastLoginAt = lastLoginAt,
        };

        user.RestorePersistence(id, externalId, createdAt, updatedAt);

        return user;
    }

    private static string ValidateLogin(string login)
    {
        if (string.IsNullOrWhiteSpace(login))
        {
            throw new DomainException("Login is required");
        }

        return login.Trim();
    }

    private static string ValidateFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("Full name is required");
        }

        return fullName.Trim();
    }

    private static string ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            throw new DomainException("A valid email is required");
        }

        return email.Trim();
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
