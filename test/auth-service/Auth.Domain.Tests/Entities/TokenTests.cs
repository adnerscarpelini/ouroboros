namespace Ouroboros.Auth.Domain.Entities;

using Ouroboros.Auth.Domain.Exceptions;
using Xunit;

public class TokenTests
{
    [Fact]
    public void ShouldCreateUnusedTokenWhenDataIsValid()
    {
        var userExternalId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);

        var token = Token.Create(userExternalId, TokenType.EmailConfirmation, "token-hash", expiresAt);

        Assert.NotEqual(Guid.Empty, token.ExternalId);
        Assert.Equal(userExternalId, token.UserExternalId);
        Assert.Equal(TokenType.EmailConfirmation, token.Type);
        Assert.Equal("token-hash", token.TokenHash);
        Assert.Equal(expiresAt, token.ExpiresAt);
        Assert.Null(token.UsedAt);
    }

    [Fact]
    public void ShouldThrowDomainExceptionWhenUserIsEmpty()
    {
        Assert.Throws<DomainException>(() => Token.Create(Guid.Empty, TokenType.EmailConfirmation, "token-hash", DateTimeOffset.UtcNow.AddHours(1)));
    }

    [Fact]
    public void ShouldThrowDomainExceptionWhenTypeIsInvalid()
    {
        Assert.Throws<DomainException>(() => Token.Create(Guid.NewGuid(), (TokenType)999, "token-hash", DateTimeOffset.UtcNow.AddHours(1)));
    }

    [Fact]
    public void ShouldThrowDomainExceptionWhenTokenHashIsInvalid()
    {
        Assert.Throws<DomainException>(() => Token.Create(Guid.NewGuid(), TokenType.EmailConfirmation, "   ", DateTimeOffset.UtcNow.AddHours(1)));
    }

    [Fact]
    public void ShouldThrowDomainExceptionWhenExpirationIsInThePast()
    {
        Assert.Throws<DomainException>(() => Token.Create(Guid.NewGuid(), TokenType.EmailConfirmation, "token-hash", DateTimeOffset.UtcNow.AddMinutes(-1)));
    }
}
