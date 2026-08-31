namespace Ouroboros.Modules.Auth.Infrastructure.Tests;

public class Argon2PasswordHasherTests
{
	[Fact]
	public void Verify_WithCorrectPassword_ReturnsTrue()
	{
		var hasher = new Argon2PasswordHasher();
		var hash = hasher.Hash("S3nhaF0rte!");

		Assert.True(hasher.Verify(hash, "S3nhaF0rte!"));
	}

	[Fact]
	public void Verify_WithWrongPassword_ReturnsFalse()
	{
		var hasher = new Argon2PasswordHasher();
		var hash = hasher.Hash("S3nhaF0rte!");

		Assert.False(hasher.Verify(hash, "senha-errada"));
	}

	[Fact]
	public void Hash_NeverReturnsThePlainPassword()
	{
		var hasher = new Argon2PasswordHasher();

		var hash = hasher.Hash("S3nhaF0rte!");

		Assert.NotEqual("S3nhaF0rte!", hash);
	}
}
