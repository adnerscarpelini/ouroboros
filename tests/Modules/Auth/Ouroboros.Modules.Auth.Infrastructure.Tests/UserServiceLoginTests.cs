using Microsoft.EntityFrameworkCore;
using Ouroboros.Modules.Auth.Domain;
using static Ouroboros.Modules.Auth.Infrastructure.Tests.UserServiceTestHelpers;

namespace Ouroboros.Modules.Auth.Infrastructure.Tests;

public class UserServiceLoginTests
{
	private const string CorrectPassword = "correct-password";

	private static async Task<User> SeedUserAsync(
		AuthDbContext dbContext,
		bool isActive = true
	)
	{
		var user = new User(
			login: "jsilva",
			fullName: "João Silva",
			email: "joao.silva@example.com",
			passwordHash: new FakePasswordHasher().Hash(CorrectPassword)
		);

		if (isActive)
		{
			user.ConfirmEmail();
		}

		dbContext.Users.Add(user);
		await dbContext.SaveChangesAsync();

		return user;
	}

	[Fact]
	public async Task LoginAsync_WithCorrectCredentials_ReturnsAccessTokenAndResetsAttempts()
	{
		await using var dbContext = CreateDbContext();
		var user = await SeedUserAsync(dbContext);
		user.RegisterFailedLoginAttempt();
		await dbContext.SaveChangesAsync();

		var userService = CreateUserService(dbContext);

		var result = await userService.LoginAsync("jsilva", CorrectPassword, CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal("token-for:jsilva", result.Value!.AccessToken);

		var reloadedUser = await dbContext.Users.SingleAsync();
		Assert.Equal(0, reloadedUser.FailedLoginAttempts);
		Assert.NotNull(reloadedUser.LastLoginAt);
	}

	[Fact]
	public async Task LoginAsync_WithUnknownLogin_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		var userService = CreateUserService(dbContext);

		var result = await userService.LoginAsync("nao-existe", "any-password", CancellationToken.None);

		Assert.False(result.IsSuccess);
	}

	[Fact]
	public async Task LoginAsync_WithWrongPassword_ReturnsFailureAndRegistersAttempt()
	{
		await using var dbContext = CreateDbContext();
		await SeedUserAsync(dbContext);

		var userService = CreateUserService(dbContext);

		var result = await userService.LoginAsync("jsilva", "senha-errada", CancellationToken.None);

		Assert.False(result.IsSuccess);

		var reloadedUser = await dbContext.Users.SingleAsync();
		Assert.Equal(1, reloadedUser.FailedLoginAttempts);
	}

	[Fact]
	public async Task LoginAsync_WithInactiveUser_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		await SeedUserAsync(dbContext, isActive: false);

		var userService = CreateUserService(dbContext);

		var result = await userService.LoginAsync("jsilva", CorrectPassword, CancellationToken.None);

		Assert.False(result.IsSuccess);
	}

	[Fact]
	public async Task LoginAsync_WithLockedUser_ReturnsFailureWithoutCheckingPassword()
	{
		await using var dbContext = CreateDbContext();
		var user = await SeedUserAsync(dbContext);

		for (var i = 0; i < 5; i++)
		{
			user.RegisterFailedLoginAttempt();
		}
		await dbContext.SaveChangesAsync();

		var userService = CreateUserService(dbContext);

		var result = await userService.LoginAsync("jsilva", CorrectPassword, CancellationToken.None);

		Assert.False(result.IsSuccess);
	}
}
