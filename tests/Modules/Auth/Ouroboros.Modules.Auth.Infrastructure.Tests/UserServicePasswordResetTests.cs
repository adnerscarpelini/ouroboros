using Microsoft.EntityFrameworkCore;
using Ouroboros.Modules.Auth.Domain;
using static Ouroboros.Modules.Auth.Infrastructure.Tests.UserServiceTestHelpers;

namespace Ouroboros.Modules.Auth.Infrastructure.Tests;

public class UserServicePasswordResetTests
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

	private static async Task<Token> SeedPasswordResetTokenAsync(
		AuthDbContext dbContext,
		User user,
		string tokenHash,
		DateTime expiresAt,
		bool validated = false
	)
	{
		var tokenType = await dbContext.TokenTypes.SingleAsync(t => t.Name == TokenTypeNames.PasswordReset);

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

		return token;
	}

	[Fact]
	public async Task RequestPasswordResetAsync_WithExistingEmail_EnqueuesEmailAndCreatesToken()
	{
		await using var dbContext = CreateDbContext();
		await SeedUserAsync(dbContext);

		var emailQueueService = new FakeEmailQueueService();
		var userService = CreateUserService(dbContext, emailQueueService);

		await userService.RequestPasswordResetAsync("joao.silva@example.com", CancellationToken.None);

		Assert.Equal("joao.silva@example.com", emailQueueService.LastRecipient);

		var tokenType = await dbContext.TokenTypes.SingleAsync(t => t.Name == TokenTypeNames.PasswordReset);
		var token = await dbContext.Tokens.SingleAsync(t => t.TokenTypeId == tokenType.Id);
		Assert.False(token.Validated);
	}

	[Fact]
	public async Task RequestPasswordResetAsync_WithUnknownEmail_DoesNotEnqueueEmailOrCreateToken()
	{
		await using var dbContext = CreateDbContext();

		var emailQueueService = new FakeEmailQueueService();
		var userService = CreateUserService(dbContext, emailQueueService);

		await userService.RequestPasswordResetAsync("nao-existe@example.com", CancellationToken.None);

		Assert.Null(emailQueueService.LastRecipient);
		Assert.False(await dbContext.Tokens.AnyAsync());
	}

	[Fact]
	public async Task RequestPasswordResetAsync_WithPendingToken_InvalidatesPreviousToken()
	{
		await using var dbContext = CreateDbContext();
		var user = await SeedUserAsync(dbContext);
		var previousToken = await SeedPasswordResetTokenAsync(
			dbContext,
			user,
			tokenHash: "hashed:previous-token",
			expiresAt: DateTime.UtcNow.AddHours(1)
		);

		var userService = CreateUserService(dbContext);

		await userService.RequestPasswordResetAsync("joao.silva@example.com", CancellationToken.None);

		var reloadedPreviousToken = await dbContext.Tokens.SingleAsync(t => t.Id == previousToken.Id);
		Assert.True(reloadedPreviousToken.Validated);
	}

	[Fact]
	public async Task ResetPasswordAsync_WithValidToken_UpdatesPasswordAndValidatesToken()
	{
		await using var dbContext = CreateDbContext();
		var user = await SeedUserAsync(dbContext);
		await SeedPasswordResetTokenAsync(
			dbContext,
			user,
			tokenHash: "hashed:known-token",
			expiresAt: DateTime.UtcNow.AddHours(1)
		);

		var userService = CreateUserService(dbContext);

		var result = await userService.ResetPasswordAsync("known-token", "new-password", CancellationToken.None);

		Assert.True(result.IsSuccess);

		var reloadedUser = await dbContext.Users.SingleAsync();
		Assert.Equal("hashed:new-password", reloadedUser.PasswordHash);

		var token = await dbContext.Tokens.SingleAsync();
		Assert.True(token.Validated);
		Assert.NotNull(token.ValidatedAt);
	}

	[Fact]
	public async Task ResetPasswordAsync_WithUnknownToken_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		var userService = CreateUserService(dbContext);

		var result = await userService.ResetPasswordAsync("unknown-token", "new-password", CancellationToken.None);

		Assert.False(result.IsSuccess);
	}

	[Fact]
	public async Task ResetPasswordAsync_WithAlreadyValidatedToken_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		var user = await SeedUserAsync(dbContext);
		await SeedPasswordResetTokenAsync(
			dbContext,
			user,
			tokenHash: "hashed:known-token",
			expiresAt: DateTime.UtcNow.AddHours(1),
			validated: true
		);

		var userService = CreateUserService(dbContext);

		var result = await userService.ResetPasswordAsync("known-token", "new-password", CancellationToken.None);

		Assert.False(result.IsSuccess);
	}

	[Fact]
	public async Task ResetPasswordAsync_WithExpiredToken_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		var user = await SeedUserAsync(dbContext);
		await SeedPasswordResetTokenAsync(
			dbContext,
			user,
			tokenHash: "hashed:known-token",
			expiresAt: DateTime.UtcNow.AddHours(-1)
		);

		var userService = CreateUserService(dbContext);

		var result = await userService.ResetPasswordAsync("known-token", "new-password", CancellationToken.None);

		Assert.False(result.IsSuccess);

		var reloadedUser = await dbContext.Users.SingleAsync();
		Assert.Equal("hashed:existing", reloadedUser.PasswordHash);
	}
}
