using Microsoft.EntityFrameworkCore;
using Ouroboros.Modules.Auth.Domain;
using static Ouroboros.Modules.Auth.Infrastructure.Tests.UserServiceTestHelpers;

namespace Ouroboros.Modules.Auth.Infrastructure.Tests;

public class UserServiceConfirmEmailTests
{
	private static async Task<(User User, Token Token)> SeedUserWithTokenAsync(
		AuthDbContext dbContext,
		string tokenHash,
		DateTime expiresAt,
		bool validated = false
	)
	{
		var user = new User(
			login: "jsilva",
			fullName: "João Silva",
			email: "joao.silva@example.com",
			passwordHash: "hashed:existing"
		);
		dbContext.Users.Add(user);
		await dbContext.SaveChangesAsync();

		var tokenType = await dbContext.TokenTypes.SingleAsync(t => t.Name == TokenTypeNames.UserCreationValidation);

		var token = new Token(
			tokenTypeId: tokenType.Id,
			userId: user.Id,
			emailMessageId: 1,
			tokenHash: tokenHash,
			expiresAt: expiresAt
		);

		if (validated)
		{
			token.Validate();
		}

		dbContext.Tokens.Add(token);
		await dbContext.SaveChangesAsync();

		return (user, token);
	}

	[Fact]
	public async Task ConfirmEmailAsync_WithValidToken_ActivatesUserAndValidatesToken()
	{
		await using var dbContext = CreateDbContext();
		await SeedUserWithTokenAsync(
			dbContext,
			tokenHash: "hashed:known-token",
			expiresAt: DateTime.UtcNow.AddHours(1)
		);

		var userService = CreateUserService(dbContext);

		var result = await userService.ConfirmEmailAsync("known-token", CancellationToken.None);

		Assert.True(result.IsSuccess);

		var user = await dbContext.Users.SingleAsync();
		Assert.True(user.IsActive);
		Assert.True(user.EmailConfirmed);

		var token = await dbContext.Tokens.SingleAsync();
		Assert.True(token.Validated);
		Assert.NotNull(token.ValidatedAt);
	}

	[Fact]
	public async Task ConfirmEmailAsync_WithUnknownToken_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		var userService = CreateUserService(dbContext);

		var result = await userService.ConfirmEmailAsync("unknown-token", CancellationToken.None);

		Assert.False(result.IsSuccess);
	}

	[Fact]
	public async Task ConfirmEmailAsync_WithAlreadyValidatedToken_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		await SeedUserWithTokenAsync(
			dbContext,
			tokenHash: "hashed:known-token",
			expiresAt: DateTime.UtcNow.AddHours(1),
			validated: true
		);

		var userService = CreateUserService(dbContext);

		var result = await userService.ConfirmEmailAsync("known-token", CancellationToken.None);

		Assert.False(result.IsSuccess);
	}

	[Fact]
	public async Task ConfirmEmailAsync_WithExpiredToken_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		await SeedUserWithTokenAsync(
			dbContext,
			tokenHash: "hashed:known-token",
			expiresAt: DateTime.UtcNow.AddHours(-1)
		);

		var userService = CreateUserService(dbContext);

		var result = await userService.ConfirmEmailAsync("known-token", CancellationToken.None);

		Assert.False(result.IsSuccess);

		var user = await dbContext.Users.SingleAsync();
		Assert.False(user.IsActive);
	}
}
