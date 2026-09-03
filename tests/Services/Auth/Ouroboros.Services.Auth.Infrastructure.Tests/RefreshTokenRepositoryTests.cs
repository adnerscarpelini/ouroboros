using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Infrastructure.Tests;

public class RefreshTokenRepositoryTests
{
	[Fact]
	public async Task GetByHashAsync_WithKnownHash_LoadsUser()
	{
		var database = new InMemoryAuthDatabase();

		await using (var writeContext = database.CreateContext())
		{
			var user = new User(
				login: "jsilva",
				fullName: "João Silva",
				email: "joao.silva@example.com",
				passwordHash: "hashed:existing"
			);

			writeContext.Users.Add(user);

			new RefreshTokenRepository(writeContext).Add(new RefreshToken(
				user: user,
				tokenHash: "hashed:known-refresh-token",
				expiresAt: DateTime.UtcNow.AddDays(1)
			));

			await writeContext.SaveChangesAsync();
		}

		await using var readContext = database.CreateContext();

		var refreshToken = await new RefreshTokenRepository(readContext).GetByHashAsync(
			tokenHash: "hashed:known-refresh-token",
			cancellationToken: CancellationToken.None
		);

		Assert.NotNull(refreshToken);
		// O caso de uso de rotação emite um novo par de tokens para este usuário — sem o Include,
		// a navegação viria nula e a rotação quebraria.
		Assert.Equal("jsilva", refreshToken.User.Login);
		Assert.Equal(refreshToken.User.Id, refreshToken.UserId);
	}

	[Fact]
	public async Task GetByHashAsync_WithUnknownHash_ReturnsNull()
	{
		var database = new InMemoryAuthDatabase();

		await using var dbContext = database.CreateContext();

		var refreshToken = await new RefreshTokenRepository(dbContext).GetByHashAsync(
			tokenHash: "hashed:unknown-token",
			cancellationToken: CancellationToken.None
		);

		Assert.Null(refreshToken);
	}
}
