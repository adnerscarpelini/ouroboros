using Microsoft.EntityFrameworkCore;
using Ouroboros.Services.Auth.Domain;
using static Ouroboros.Services.Auth.Infrastructure.Tests.UserServiceTestHelpers;

namespace Ouroboros.Services.Auth.Infrastructure.Tests;

public class UserServiceLogoutTests
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
		bool revoked = false
	)
	{
		var refreshToken = new RefreshToken(
			userId: user.Id,
			tokenHash: tokenHash,
			expiresAt: DateTime.UtcNow.AddDays(1)
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
	public async Task LogoutAsync_WithValidToken_RevokesToken()
	{
		await using var dbContext = CreateDbContext();
		var user = await SeedUserAsync(dbContext);
		var refreshToken = await SeedRefreshTokenAsync(dbContext, user, tokenHash: "hashed:known-refresh-token");

		var userService = CreateUserService(dbContext);

		var result = await userService.LogoutAsync("known-refresh-token", CancellationToken.None);

		Assert.True(result.IsSuccess);

		var reloadedToken = await dbContext.RefreshTokens.SingleAsync(t => t.Id == refreshToken.Id);
		Assert.NotNull(reloadedToken.RevokedAt);
	}

	[Fact]
	public async Task LogoutAsync_WithUnknownToken_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		var userService = CreateUserService(dbContext);

		var result = await userService.LogoutAsync("unknown-token", CancellationToken.None);

		Assert.False(result.IsSuccess);
	}

	[Fact]
	public async Task LogoutAsync_WithAlreadyRevokedToken_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		var user = await SeedUserAsync(dbContext);
		await SeedRefreshTokenAsync(dbContext, user, tokenHash: "hashed:known-refresh-token", revoked: true);

		var userService = CreateUserService(dbContext);

		var result = await userService.LogoutAsync("known-refresh-token", CancellationToken.None);

		Assert.False(result.IsSuccess);
	}
}
