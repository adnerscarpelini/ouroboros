namespace Ouroboros.Auth.Domain.Entities;

using Ouroboros.Auth.Domain.Exceptions;

public sealed class Token : Entity
{
    public Guid UserExternalId { get; private set; }

    public TokenType Type { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    private Token()
    {
    }

    public static Token Create(
        Guid userExternalId,
        TokenType type,
        string tokenHash,
        DateTimeOffset expiresAt)
    {
        var token = new Token
        {
            UserExternalId = ValidateUserExternalId(userExternalId),
            Type = ValidateType(type),
            TokenHash = ValidateTokenHash(tokenHash),
            UsedAt = null,
        };

        token.ExpiresAt = ValidateExpiresAt(expiresAt, token.CreatedAt);

        return token;
    }

    public static Token Rehydrate(
        long id,
        Guid externalId,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt,
        Guid userExternalId,
        TokenType type,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset? usedAt)
    {
        var token = new Token
        {
            UserExternalId = userExternalId,
            Type = type,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            UsedAt = usedAt,
        };

        token.RestorePersistence(id, externalId, createdAt, updatedAt);

        return token;
    }

    public void MarkAsUsed(DateTimeOffset usedAt)
    {
        if (UsedAt is not null)
        {
            throw new DomainException("Token has already been used");
        }

        if (usedAt >= ExpiresAt)
        {
            throw new DomainException("Token has expired");
        }

        UsedAt = usedAt;
        MarkAsUpdated();
    }

    private static Guid ValidateUserExternalId(Guid userExternalId)
    {
        if (userExternalId == Guid.Empty)
        {
            throw new DomainException("User is required");
        }

        return userExternalId;
    }

    private static TokenType ValidateType(TokenType type)
    {
        if (!Enum.IsDefined(type))
        {
            throw new DomainException("A valid token type is required");
        }

        return type;
    }

    private static string ValidateTokenHash(string tokenHash)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("Token hash is required");
        }

        return tokenHash;
    }

    private static DateTimeOffset ValidateExpiresAt(DateTimeOffset expiresAt, DateTimeOffset createdAt)
    {
        if (expiresAt <= createdAt)
        {
            throw new DomainException("Token expiration must be in the future");
        }

        return expiresAt;
    }
}
