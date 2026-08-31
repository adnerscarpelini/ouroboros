namespace Ouroboros.Modules.Auth.Domain.Tests;

public class UserTests
{
	[Fact]
	public void Constructor_CreatesInactiveUnconfirmedUser()
	{
		var user = new User(
			login: "jsilva",
			fullName: "João Silva",
			email: "joao.silva@example.com",
			passwordHash: "hashed-password"
		);

		Assert.False(user.IsActive);
		Assert.False(user.EmailConfirmed);
		Assert.Equal(0, user.FailedLoginAttempts);
		Assert.Null(user.LockedUntil);
		Assert.Null(user.LastLoginAt);
		Assert.Equal(user.CreatedAt, user.PasswordChangedAt);
	}
}
