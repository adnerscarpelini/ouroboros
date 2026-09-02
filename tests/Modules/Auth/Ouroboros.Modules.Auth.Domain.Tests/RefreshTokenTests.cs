namespace Ouroboros.Modules.Auth.Domain.Tests;

public class RefreshTokenTests
{
	private static RefreshToken CreateRefreshToken()
	{
		return new RefreshToken(
			userId: 1,
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
	public void Revoke_SetsRevokedAt()
	{
		var refreshToken = CreateRefreshToken();

		refreshToken.Revoke();

		Assert.NotNull(refreshToken.RevokedAt);
	}
}
