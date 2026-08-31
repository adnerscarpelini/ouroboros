using Microsoft.EntityFrameworkCore;

namespace Ouroboros.Modules.Auth.Infrastructure.Tests;

internal static class UserServiceTestHelpers
{
	public static AuthDbContext CreateDbContext()
	{
		var options = new DbContextOptionsBuilder<AuthDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;

		var dbContext = new AuthDbContext(options);
		dbContext.Database.EnsureCreated();

		return dbContext;
	}

	public static UserService CreateUserService(
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
}
