using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Infrastructure.Tests;

public class UserRepositoryTests
{
	private static User CreateUser(
		string login = "jsilva",
		string email = "joao.silva@example.com"
	)
	{
		return new User(
			login: login,
			fullName: "João Silva",
			email: email,
			passwordHash: "hashed:existing"
		);
	}

	[Fact]
	public async Task Add_PersistsUserOnSaveChanges()
	{
		var database = new InMemoryAuthDatabase();

		await using (var writeContext = database.CreateContext())
		{
			new UserRepository(writeContext).Add(CreateUser());

			await writeContext.SaveChangesAsync();
		}

		await using var readContext = database.CreateContext();

		var user = await new UserRepository(readContext).GetByLoginAsync(
			login: "jsilva",
			cancellationToken: CancellationToken.None
		);

		Assert.NotNull(user);
		Assert.NotEqual(0, user.Id);
	}

	[Fact]
	public async Task GetByLoginAsync_WithUnknownLogin_ReturnsNull()
	{
		var database = new InMemoryAuthDatabase();

		await using var dbContext = database.CreateContext();

		var user = await new UserRepository(dbContext).GetByLoginAsync(
			login: "nao-existe",
			cancellationToken: CancellationToken.None
		);

		Assert.Null(user);
	}

	[Fact]
	public async Task GetByEmailAsync_WithKnownEmail_ReturnsUser()
	{
		var database = new InMemoryAuthDatabase();

		await using (var writeContext = database.CreateContext())
		{
			new UserRepository(writeContext).Add(CreateUser());

			await writeContext.SaveChangesAsync();
		}

		await using var readContext = database.CreateContext();

		var user = await new UserRepository(readContext).GetByEmailAsync(
			email: "joao.silva@example.com",
			cancellationToken: CancellationToken.None
		);

		Assert.NotNull(user);
		Assert.Equal("jsilva", user.Login);
	}

	[Fact]
	public async Task ExistsByLoginAndEmailAsync_ReflectWhatIsPersisted()
	{
		var database = new InMemoryAuthDatabase();

		await using (var writeContext = database.CreateContext())
		{
			new UserRepository(writeContext).Add(CreateUser());

			await writeContext.SaveChangesAsync();
		}

		await using var readContext = database.CreateContext();
		var userRepository = new UserRepository(readContext);

		Assert.True(await userRepository.ExistsByLoginAsync(
			login: "jsilva",
			cancellationToken: CancellationToken.None
		));

		Assert.True(await userRepository.ExistsByEmailAsync(
			email: "joao.silva@example.com",
			cancellationToken: CancellationToken.None
		));

		Assert.False(await userRepository.ExistsByLoginAsync(
			login: "outrologin",
			cancellationToken: CancellationToken.None
		));

		Assert.False(await userRepository.ExistsByEmailAsync(
			email: "outro@example.com",
			cancellationToken: CancellationToken.None
		));
	}
}
