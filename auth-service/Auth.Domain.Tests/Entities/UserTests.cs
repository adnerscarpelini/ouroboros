namespace Ouroboros.Auth.Domain.Entities;

using Ouroboros.Auth.Domain.Exceptions;
using Xunit;

public class UserTests
{
    [Fact]
    public void ShouldCreateUserInactiveByDefaultWhenDataIsValid()
    {
        var user = User.Create("jdoe", "John Doe", "jdoe@example.com", "hashed-password");

        Assert.NotEqual(Guid.Empty, user.ExternalId);
        Assert.Equal("jdoe", user.Login);
        Assert.Equal("John Doe", user.FullName);
        Assert.Equal("jdoe@example.com", user.Email);
        Assert.Equal("hashed-password", user.PasswordHash);
        Assert.False(user.EmailConfirmed);
        Assert.False(user.Active);
        Assert.Null(user.LastLoginAt);
        Assert.NotEqual(default, user.PasswordChangedAt);
    }

    [Fact]
    public void ShouldThrowDomainExceptionWhenLoginIsInvalid()
    {
        Assert.Throws<DomainException>(() => User.Create("   ", "John Doe", "jdoe@example.com", "hashed-password"));
    }

    [Fact]
    public void ShouldThrowDomainExceptionWhenFullNameIsInvalid()
    {
        Assert.Throws<DomainException>(() => User.Create("jdoe", "   ", "jdoe@example.com", "hashed-password"));
    }

    [Fact]
    public void ShouldThrowDomainExceptionWhenEmailIsInvalid()
    {
        Assert.Throws<DomainException>(() => User.Create("jdoe", "John Doe", "not-an-email", "hashed-password"));
    }

    [Fact]
    public void ShouldThrowDomainExceptionWhenPasswordHashIsInvalid()
    {
        Assert.Throws<DomainException>(() => User.Create("jdoe", "John Doe", "jdoe@example.com", "   "));
    }
}
