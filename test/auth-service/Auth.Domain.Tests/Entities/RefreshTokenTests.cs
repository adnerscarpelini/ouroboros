namespace Ouroboros.Auth.Domain.Entities;

using Ouroboros.Auth.Domain.Exceptions;
using Xunit;

public class RefreshTokenTests
{
    [Fact]
    public void ShouldCreateActiveRefreshTokenWhenDataIsValid()
    {
        var userExternalId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

        var refreshToken = RefreshToken.Create(userExternalId, "token-hash", expiresAt);

        Assert.NotEqual(Guid.Empty, refreshToken.ExternalId);
        Assert.Equal(userExternalId, refreshToken.UserExternalId);
        Assert.Equal("token-hash", refreshToken.TokenHash);
        Assert.Equal(expiresAt, refreshToken.ExpiresAt);
        Assert.Null(refreshToken.RevokedAt);
    }

    [Fact]
    public void ShouldThrowDomainExceptionWhenUserIsEmpty()
    {
        Assert.Throws<DomainException>(() => RefreshToken.Create(Guid.Empty, "token-hash", DateTimeOffset.UtcNow.AddDays(7)));
    }

    [Fact]
    public void ShouldThrowDomainExceptionWhenTokenHashIsInvalid()
    {
        Assert.Throws<DomainException>(() => RefreshToken.Create(Guid.NewGuid(), "   ", DateTimeOffset.UtcNow.AddDays(7)));
    }

    [Fact]
    public void ShouldThrowDomainExceptionWhenExpirationIsInThePast()
    {
        Assert.Throws<DomainException>(() => RefreshToken.Create(Guid.NewGuid(), "token-hash", DateTimeOffset.UtcNow.AddMinutes(-1)));
    }
}
