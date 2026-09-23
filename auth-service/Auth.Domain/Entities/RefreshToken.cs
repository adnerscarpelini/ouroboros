namespace Ouroboros.Auth.Domain.Entities;

using Ouroboros.Auth.Domain.Exceptions;

public sealed class RefreshToken : Entity
{
    public Guid UserExternalId { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    private RefreshToken()
    {
    }

    public static RefreshToken Create(
        Guid userExternalId,
        string tokenHash,
        DateTimeOffset expiresAt)
    {
        var refreshToken = new RefreshToken
        {
            UserExternalId = ValidateUserExternalId(userExternalId),
            TokenHash = ValidateTokenHash(tokenHash),
            RevokedAt = null,
        };

        refreshToken.ExpiresAt = ValidateExpiresAt(expiresAt, refreshToken.CreatedAt);

        return refreshToken;
    }

    public static RefreshToken Rehydrate(
        long id,
        Guid externalId,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt,
        Guid userExternalId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset? revokedAt)
    {
        var refreshToken = new RefreshToken
        {
            UserExternalId = userExternalId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            RevokedAt = revokedAt,
        };

        refreshToken.RestorePersistence(id, externalId, createdAt, updatedAt);

        return refreshToken;
    }

    public void Revoke(DateTimeOffset revokedAt)
    {
        if (RevokedAt is not null)
        {
            throw new InvalidRefreshTokenException("Refresh token has been revoked");
        }

        if (revokedAt >= ExpiresAt)
        {
            throw new InvalidRefreshTokenException("Refresh token has expired");
        }

        RevokedAt = revokedAt;
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
