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

		return new AuthDbContext(options);
	}

	[Fact]
	public async Task CreateUserAsync_WithNewLoginAndEmail_CreatesUserAndReturnsSuccess()
	{
		await using var dbContext = CreateDbContext();
		var userService = new UserService(dbContext, new FakePasswordHasher());

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

		var userService = new UserService(dbContext, new FakePasswordHasher());

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

		var userService = new UserService(dbContext, new FakePasswordHasher());

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
