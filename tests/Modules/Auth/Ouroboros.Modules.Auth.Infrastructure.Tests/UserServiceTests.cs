using Microsoft.EntityFrameworkCore;
using Ouroboros.Modules.Auth.Domain;

namespace Ouroboros.Modules.Auth.Infrastructure.Tests;

public class UserServiceTests
{
	private static AuthDbContext CreateDbContext()
	{
		var options = new DbContextOptionsBuilder<AuthDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;

		var dbContext = new AuthDbContext(options);
		dbContext.Database.EnsureCreated();

		return dbContext;
	}

	private static UserService CreateUserService(
		AuthDbContext dbContext,
		FakeEmailQueueService? emailQueueService = null
	)
	{
		return new UserService(
			dbContext,
			new FakePasswordHasher(),
			new FakeTokenGenerator(),
			emailQueueService ?? new FakeEmailQueueService()
		);
	}

	[Fact]
	public async Task CreateUserAsync_WithNewLoginAndEmail_CreatesUserAndReturnsSuccess()
	{
		await using var dbContext = CreateDbContext();
		var emailQueueService = new FakeEmailQueueService();
		var userService = CreateUserService(dbContext, emailQueueService);

		var result = await userService.CreateUserAsync(
			login: "jsilva",
			fullName: "João Silva",
			email: "joao.silva@example.com",
			password: "any-password",
			cancellationToken: CancellationToken.None
		);

		Assert.True(result.IsSuccess);
		Assert.NotEqual(Guid.Empty, result.Value);

		var createdUser = await dbContext.Users.SingleAsync();
		Assert.Equal("jsilva", createdUser.Login);
		Assert.Equal("hashed:any-password", createdUser.PasswordHash);
		Assert.Equal(result.Value, createdUser.ExternalId);
		Assert.False(createdUser.IsActive);

		var createdToken = await dbContext.Tokens.SingleAsync();
		Assert.Equal(createdUser.Id, createdToken.UserId);
		Assert.Equal("hashed:raw-token", createdToken.TokenHash);
		Assert.False(createdToken.Validated);
		Assert.True(createdToken.ExpiresAt > DateTime.UtcNow);

		Assert.Equal("joao.silva@example.com", emailQueueService.LastRecipient);
		Assert.Contains("raw-token", emailQueueService.LastBodyHtml);
	}

	[Fact]
	public async Task CreateUserAsync_WithLoginAlreadyInUse_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		dbContext.Users.Add(new User(
			login: "jsilva",
			fullName: "João Silva",
			email: "joao.silva@example.com",
			passwordHash: "hashed:existing"
		));
		await dbContext.SaveChangesAsync();

		var userService = CreateUserService(dbContext);

		var result = await userService.CreateUserAsync(
			login: "jsilva",
			fullName: "Outro Nome",
			email: "outro@example.com",
			password: "any-password",
			cancellationToken: CancellationToken.None
		);

		Assert.False(result.IsSuccess);
		Assert.Equal(1, await dbContext.Users.CountAsync());
	}

	[Fact]
	public async Task CreateUserAsync_WithEmailAlreadyInUse_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		dbContext.Users.Add(new User(
			login: "jsilva",
			fullName: "João Silva",
			email: "joao.silva@example.com",
			passwordHash: "hashed:existing"
		));
		await dbContext.SaveChangesAsync();

		var userService = CreateUserService(dbContext);

		var result = await userService.CreateUserAsync(
			login: "outrologin",
			fullName: "Outro Nome",
			email: "joao.silva@example.com",
			password: "any-password",
			cancellationToken: CancellationToken.None
		);

		Assert.False(result.IsSuccess);
		Assert.Equal(1, await dbContext.Users.CountAsync());
	}
}
