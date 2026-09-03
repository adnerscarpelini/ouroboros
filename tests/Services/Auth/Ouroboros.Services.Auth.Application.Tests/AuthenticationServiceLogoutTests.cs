namespace Ouroboros.Services.Auth.Application.Tests;

public class AuthenticationServiceLogoutTests
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

		var authenticationService = context.CreateAuthenticationService();

		var result = await authenticationService.LogoutAsync("known-refresh-token", CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.NotNull(refreshToken.RevokedAt);
	}

	[Fact]
	public async Task LogoutAsync_WithUnknownToken_ReturnsFailure()
	{
		var context = new AuthTestContext();
		var authenticationService = context.CreateAuthenticationService();

		var result = await authenticationService.LogoutAsync("unknown-token", CancellationToken.None);

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

		var authenticationService = context.CreateAuthenticationService();

		var result = await authenticationService.LogoutAsync("known-refresh-token", CancellationToken.None);

		Assert.False(result.IsSuccess);
	}
}
