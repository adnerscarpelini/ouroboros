using Microsoft.EntityFrameworkCore;
using Ouroboros.Services.Auth.Domain;
using static Ouroboros.Services.Auth.Infrastructure.Tests.UserServiceTestHelpers;

namespace Ouroboros.Services.Auth.Infrastructure.Tests;

public class UserServiceRefreshTokenTests
{
	private static async Task<User> SeedUserAsync(AuthDbContext dbContext)
	{
		var user = new User(
			login: "jsilva",
			fullName: "João Silva",
			email: "joao.silva@example.com",
			passwordHash: "hashed:existing"
		);
		user.ConfirmEmail();

		dbContext.Users.Add(user);
		await dbContext.SaveChangesAsync();

		return user;
	}

	private static async Task<RefreshToken> SeedRefreshTokenAsync(
		AuthDbContext dbContext,
		User user,
		string tokenHash,
		DateTime expiresAt,
		bool revoked = false
	)
	{
		var refreshToken = new RefreshToken(
			userId: user.Id,
			tokenHash: tokenHash,
			expiresAt: expiresAt
		);

		if (revoked)
		{
			refreshToken.Revoke();
		}

		dbContext.RefreshTokens.Add(refreshToken);
		await dbContext.SaveChangesAsync();

		return refreshToken;
	}

	[Fact]
	public async Task RefreshTokenAsync_WithValidToken_RotatesAndReturnsNewAuthenticationResult()
	{
		await using var dbContext = CreateDbContext();
		var user = await SeedUserAsync(dbContext);
		var previousToken = await SeedRefreshTokenAsync(
			dbContext,
			user,
			tokenHash: "hashed:known-refresh-token",
			expiresAt: DateTime.UtcNow.AddDays(1)
		);

		var userService = CreateUserService(dbContext);

		var result = await userService.RefreshTokenAsync("known-refresh-token", CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal("token-for:jsilva", result.Value!.AccessToken);
		Assert.NotEqual("known-refresh-token", result.Value.RefreshToken);

		var reloadedPreviousToken = await dbContext.RefreshTokens.SingleAsync(t => t.Id == previousToken.Id);
		Assert.NotNull(reloadedPreviousToken.RevokedAt);

		Assert.Equal(2, await dbContext.RefreshTokens.CountAsync());
	}

	[Fact]
	public async Task RefreshTokenAsync_WithUnknownToken_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		var userService = CreateUserService(dbContext);

		var result = await userService.RefreshTokenAsync("unknown-token", CancellationToken.None);

		Assert.False(result.IsSuccess);
	}

	[Fact]
	public async Task RefreshTokenAsync_WithRevokedToken_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		var user = await SeedUserAsync(dbContext);
		await SeedRefreshTokenAsync(
			dbContext,
			user,
			tokenHash: "hashed:known-refresh-token",
			expiresAt: DateTime.UtcNow.AddDays(1),
			revoked: true
		);

		var userService = CreateUserService(dbContext);

		var result = await userService.RefreshTokenAsync("known-refresh-token", CancellationToken.None);

		Assert.False(result.IsSuccess);
	}

	[Fact]
	public async Task RefreshTokenAsync_WithExpiredToken_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		var user = await SeedUserAsync(dbContext);
		await SeedRefreshTokenAsync(
			dbContext,
			user,
			tokenHash: "hashed:known-refresh-token",
			expiresAt: DateTime.UtcNow.AddDays(-1)
		);

		var userService = CreateUserService(dbContext);

		var result = await userService.RefreshTokenAsync("known-refresh-token", CancellationToken.None);

		Assert.False(result.IsSuccess);
	}
}
