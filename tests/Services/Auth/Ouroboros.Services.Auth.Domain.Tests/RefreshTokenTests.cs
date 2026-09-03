namespace Ouroboros.Services.Auth.Domain.Tests;

public class RefreshTokenTests
{
	private static RefreshToken CreateRefreshToken()
	{
		return new RefreshToken(
			user: new User(
				login: "jsilva",
				fullName: "João Silva",
				email: "joao.silva@example.com",
				passwordHash: "hashed:existing"
			),
			tokenHash: "hash",
			expiresAt: DateTime.UtcNow.AddDays(30)
		);
	}

	[Fact]
	public void Constructor_CreatesNonRevokedToken()
	{
		var refreshToken = CreateRefreshToken();

		Assert.Null(refreshToken.RevokedAt);
	}

	[Fact]
	public void Constructor_KeepsUserReference()
	{
		var refreshToken = CreateRefreshToken();

		Assert.Equal("jsilva", refreshToken.User.Login);
	}

	[Fact]
	public void Revoke_SetsRevokedAt()
	{
		var refreshToken = CreateRefreshToken();

		refreshToken.Revoke();

		Assert.NotNull(refreshToken.RevokedAt);
	}
}
