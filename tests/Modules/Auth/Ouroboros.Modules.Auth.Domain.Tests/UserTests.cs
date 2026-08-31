namespace Ouroboros.Modules.Auth.Domain.Tests;

public class UserTests
{
	private static User CreateUser()
	{
		return new User(
			login: "jsilva",
			fullName: "João Silva",
			email: "joao.silva@example.com",
			passwordHash: "hashed-password"
		);
	}

	[Fact]
	public void Constructor_CreatesInactiveUnconfirmedUser()
	{
		var user = CreateUser();

		Assert.False(user.IsActive);
		Assert.False(user.EmailConfirmed);
		Assert.Equal(0, user.FailedLoginAttempts);
		Assert.Null(user.LockedUntil);
		Assert.Null(user.LastLoginAt);
		Assert.Equal(user.CreatedAt, user.PasswordChangedAt);
	}

	[Fact]
	public void ConfirmEmail_ActivatesUserAndMarksEmailAsConfirmed()
	{
		var user = CreateUser();

		user.ConfirmEmail();

		Assert.True(user.IsActive);
		Assert.True(user.EmailConfirmed);
	}

	[Fact]
	public void RegisterFailedLoginAttempt_IncrementsCounterWithoutLockingBeforeThreshold()
	{
		var user = CreateUser();

		user.RegisterFailedLoginAttempt();
		user.RegisterFailedLoginAttempt();

		Assert.Equal(2, user.FailedLoginAttempts);
		Assert.False(user.IsLockedOut());
	}

	[Fact]
	public void RegisterFailedLoginAttempt_LocksUserAfterReachingThreshold()
	{
		var user = CreateUser();

		for (var i = 0; i < 5; i++)
		{
			user.RegisterFailedLoginAttempt();
		}

		Assert.True(user.IsLockedOut());
		Assert.Equal(0, user.FailedLoginAttempts);
		Assert.NotNull(user.LockedUntil);
	}

	[Fact]
	public void RegisterSuccessfulLogin_ResetsAttemptsAndLockAndSetsLastLoginAt()
	{
		var user = CreateUser();
		user.RegisterFailedLoginAttempt();
		user.RegisterFailedLoginAttempt();

		user.RegisterSuccessfulLogin();

		Assert.Equal(0, user.FailedLoginAttempts);
		Assert.Null(user.LockedUntil);
		Assert.False(user.IsLockedOut());
		Assert.NotNull(user.LastLoginAt);
	}

	[Fact]
	public void ResetPassword_UpdatesPasswordHashAndPasswordChangedAt()
	{
		var user = CreateUser();

		user.ResetPassword("hashed-new-password");

		Assert.Equal("hashed-new-password", user.PasswordHash);
		Assert.NotEqual(user.CreatedAt, user.PasswordChangedAt);
	}

	[Fact]
	public void ResetPassword_ClearsFailedAttemptsAndLockout()
	{
		var user = CreateUser();
		for (var i = 0; i < 5; i++)
		{
			user.RegisterFailedLoginAttempt();
		}
		Assert.True(user.IsLockedOut());

		user.ResetPassword("hashed-new-password");

		Assert.Equal(0, user.FailedLoginAttempts);
		Assert.Null(user.LockedUntil);
		Assert.False(user.IsLockedOut());
	}
}
