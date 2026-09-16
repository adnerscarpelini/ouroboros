namespace Ouroboros.AuthService.Application.Tests;

public class LogoutUseCaseTests
{
	[Fact]
	public async Task LogoutAsync_WithValidToken_RevokesToken()
	{
		var context = new AuthTestContext();
		var user = context.AddUser();
		var refreshToken = context.AddRefreshToken(
			user: user,
			tokenHash: "hashed:known-refresh-token",
			expiresAt: DateTime.UtcNow.AddDays(1)
		);

		var logoutUseCase = context.CreateLogoutUseCase();

		var result = await logoutUseCase.LogoutAsync("known-refresh-token", CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.NotNull(refreshToken.RevokedAt);
	}

	[Fact]
	public async Task LogoutAsync_WithUnknownToken_ReturnsFailure()
	{
		var context = new AuthTestContext();
		var logoutUseCase = context.CreateLogoutUseCase();

		var result = await logoutUseCase.LogoutAsync("unknown-token", CancellationToken.None);

		Assert.False(result.IsSuccess);
	}

	[Fact]
	public async Task LogoutAsync_WithAlreadyRevokedToken_ReturnsFailure()
	{
		var context = new AuthTestContext();
		var user = context.AddUser();
		context.AddRefreshToken(
			user: user,
			tokenHash: "hashed:known-refresh-token",
			expiresAt: DateTime.UtcNow.AddDays(1),
			revoked: true
		);

		var logoutUseCase = context.CreateLogoutUseCase();

		var result = await logoutUseCase.LogoutAsync("known-refresh-token", CancellationToken.None);

		Assert.False(result.IsSuccess);
	}
}
