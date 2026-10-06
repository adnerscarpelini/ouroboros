namespace Ouroboros.Auth.Domain.Entities;

using Ouroboros.Auth.Domain.Exceptions;
using Xunit;

public class UserTests
{
    [Fact]
    public void ShouldLockForFifteenMinutesOnFifthFailedAccess()
    {
        var user = User.Create("jdoe", "John Doe", "jdoe@example.com", "hashed-password");
        var now = DateTimeOffset.UtcNow;

        for (var attempt = 1; attempt < 5; attempt++)
        {
            Assert.False(user.RecordFailedAccess(now));
            Assert.Equal(attempt, user.AccessFailedCount);
        }

        Assert.True(user.RecordFailedAccess(now));
        Assert.Equal(0, user.AccessFailedCount);
        Assert.Equal(now.AddMinutes(15), user.LockoutEnd);
    }

    [Fact]
    public void ShouldAllowAccessAfterLockoutExpires()
    {
        var user = User.Create("jdoe", "John Doe", "jdoe@example.com", "hashed-password");
        var now = DateTimeOffset.UtcNow;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            user.RecordFailedAccess(now);
        }

        Assert.True(user.IsLockedOut(now.AddMinutes(14)));
        Assert.False(user.IsLockedOut(now.AddMinutes(15)));
        Assert.False(user.RecordFailedAccess(now.AddMinutes(15)));
        Assert.Equal(1, user.AccessFailedCount);
    }

    [Fact]
    public void ShouldResetFailedAccessAfterSuccess()
    {
        var user = User.Create("jdoe", "John Doe", "jdoe@example.com", "hashed-password");
        user.RecordFailedAccess(DateTimeOffset.UtcNow);

        user.ResetFailedAccess();

        Assert.Equal(0, user.AccessFailedCount);
        Assert.Null(user.LockoutEnd);
    }

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
            UserRole.Admin,
            null);

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

    [Fact]
    public void ShouldThrowDomainExceptionWhenUserIsAlreadyDeleted()
    {
        var user = User.Create("jdoe", "John Doe", "jdoe@example.com", "hashed-password");
        user.Delete();
        var deletedAt = user.DeletedAt;

        Assert.Throws<DomainException>(() => user.Delete());
        Assert.Equal(deletedAt, user.DeletedAt);
    }

    [Fact]
    public void ShouldRecordLastLoginAndClearFailuresWhenLoginIsRegistered()
    {
        var user = User.Create("jdoe", "John Doe", "jdoe@example.com", "hash");
        var now = DateTimeOffset.UtcNow;
        user.RecordFailedAccess(now);

        user.RegisterLogin(now);

        Assert.Equal(now, user.LastLoginAt);
        Assert.Equal(0, user.AccessFailedCount);
        Assert.Null(user.LockoutEnd);
    }

    [Fact]
    public void ShouldHaveNoLastLoginWhenUserIsCreated()
    {
        var user = User.Create("jdoe", "John Doe", "jdoe@example.com", "hash");

        Assert.Null(user.LastLoginAt);
    }
}
