namespace Ouroboros.AuthService.Application.Tests;

public class RefreshTokenUseCaseTests
{
	[Fact]
	public async Task RefreshTokenAsync_WithValidToken_RotatesAndReturnsNewAuthenticationResult()
	{
		var context = new AuthTestContext();
		var user = context.AddUser();
		var previousToken = context.AddRefreshToken(
			user: user,
			tokenHash: "hashed:known-refresh-token",
			expiresAt: DateTime.UtcNow.AddDays(1)
		);

		var refreshTokenUseCase = context.CreateRefreshTokenUseCase();

		var result = await refreshTokenUseCase.RefreshTokenAsync("known-refresh-token", CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal("token-for:jsilva", result.Value!.AccessToken);
		Assert.NotEqual("known-refresh-token", result.Value.RefreshToken);

		Assert.NotNull(previousToken.RevokedAt);
		Assert.Equal(2, context.RefreshTokenRepository.RefreshTokens.Count);
	}

	[Fact]
	public async Task RefreshTokenAsync_WithUnknownToken_ReturnsFailure()
	{
		var context = new AuthTestContext();
		var refreshTokenUseCase = context.CreateRefreshTokenUseCase();

		var result = await refreshTokenUseCase.RefreshTokenAsync("unknown-token", CancellationToken.None);

		Assert.False(result.IsSuccess);
	}

	[Fact]
	public async Task RefreshTokenAsync_WithRevokedToken_ReturnsFailure()
	{
		var context = new AuthTestContext();
		var user = context.AddUser();
		context.AddRefreshToken(
			user: user,
			tokenHash: "hashed:known-refresh-token",
			expiresAt: DateTime.UtcNow.AddDays(1),
			revoked: true
		);

		var refreshTokenUseCase = context.CreateRefreshTokenUseCase();

		var result = await refreshTokenUseCase.RefreshTokenAsync("known-refresh-token", CancellationToken.None);

		Assert.False(result.IsSuccess);
		Assert.Single(context.RefreshTokenRepository.RefreshTokens);
	}

	[Fact]
	public async Task RefreshTokenAsync_WithExpiredToken_ReturnsFailure()
	{
		var context = new AuthTestContext();
		var user = context.AddUser();
		context.AddRefreshToken(
			user: user,
			tokenHash: "hashed:known-refresh-token",
			expiresAt: DateTime.UtcNow.AddDays(-1)
		);

		var refreshTokenUseCase = context.CreateRefreshTokenUseCase();

		var result = await refreshTokenUseCase.RefreshTokenAsync("known-refresh-token", CancellationToken.None);

		Assert.False(result.IsSuccess);
		Assert.Single(context.RefreshTokenRepository.RefreshTokens);
	}
}
