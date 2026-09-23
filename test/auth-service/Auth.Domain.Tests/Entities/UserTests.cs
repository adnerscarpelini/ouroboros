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
    public void ShouldCreateUserWithUserRoleWhenDataIsValid()
    {
        var user = User.Create("jdoe", "John Doe", "jdoe@example.com", "hashed-password");

        Assert.Equal(UserRole.User, user.Role);
    }

    [Fact]
    public void ShouldRestoreRoleWhenUserIsRehydrated()
    {
        var user = User.Rehydrate(
            1,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(-30),
            null,
            "jdoe",
            "John Doe",
            "jdoe@example.com",
            true,
            "hashed-password",
            DateTimeOffset.UtcNow.AddDays(-30),
            true,
            null,
            UserRole.Admin);

        Assert.Equal(UserRole.Admin, user.Role);
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

    [Fact]
    public void ShouldUpdatePasswordHashAndChangedAtWhenPasswordIsChanged()
    {
        var user = User.Create("jdoe", "John Doe", "jdoe@example.com", "hashed-password");
        var previousPasswordChangedAt = user.PasswordChangedAt;

        user.ChangePassword("new-hashed-password");

        Assert.Equal("new-hashed-password", user.PasswordHash);
        Assert.True(user.PasswordChangedAt >= previousPasswordChangedAt);
        Assert.NotNull(user.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ShouldThrowDomainExceptionWhenChangedPasswordHashIsInvalid(string passwordHash)
    {
        var user = User.Create("jdoe", "John Doe", "jdoe@example.com", "hashed-password");

        Assert.Throws<DomainException>(() => user.ChangePassword(passwordHash));
        Assert.Equal("hashed-password", user.PasswordHash);
        Assert.Null(user.UpdatedAt);
    }
}
