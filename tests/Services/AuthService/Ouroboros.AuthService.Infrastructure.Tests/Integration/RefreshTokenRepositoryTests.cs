using Ouroboros.AuthService.Domain;
using Ouroboros.AuthService.Infrastructure;

namespace Ouroboros.AuthService.Infrastructure.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(AuthDatabaseCollection.Name)]
public sealed class RefreshTokenRepositoryTests
{
	private readonly AuthDatabaseFixture _fixture;

	public RefreshTokenRepositoryTests(AuthDatabaseFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Add_persists_refresh_token_and_makes_it_retrievable_by_hash_with_the_user_loaded()
	{
		await using var session = _fixture.CreateSession();
		var user = await AuthTestData.CreateAndPersistUserAsync(session, CancellationToken.None);
		var repository = new RefreshTokenRepository(session);
		var unitOfWork = new UnitOfWork(session);
		var tokenHash = $"hash_{Guid.NewGuid():N}";
		var refreshToken = new RefreshToken(user, tokenHash, DateTime.UtcNow.AddDays(7));

		repository.Add(refreshToken);
		await unitOfWork.SaveChangesAsync(CancellationToken.None);

		var reloaded = await repository.GetByHashAsync(tokenHash, CancellationToken.None);

		Assert.NotNull(reloaded);
		Assert.Equal(refreshToken.ExternalId, reloaded!.ExternalId);
		Assert.Equal(user.ExternalId, reloaded.User.ExternalId);
		Assert.Null(reloaded.RevokedAt);
	}

	[Fact]
	public async Task GetByHashAsync_returns_null_when_the_hash_does_not_exist()
	{
		await using var session = _fixture.CreateSession();
		var repository = new RefreshTokenRepository(session);

		var reloaded = await repository.GetByHashAsync(
			$"hash-inexistente-{Guid.NewGuid():N}",
			CancellationToken.None);

		Assert.Null(reloaded);
	}

	[Fact]
	public async Task Update_persists_revocation_and_stamps_updated_at()
	{
		await using var session = _fixture.CreateSession();
		var user = await AuthTestData.CreateAndPersistUserAsync(session, CancellationToken.None);
		var repository = new RefreshTokenRepository(session);
		var unitOfWork = new UnitOfWork(session);
		var tokenHash = $"hash_{Guid.NewGuid():N}";
		var refreshToken = new RefreshToken(user, tokenHash, DateTime.UtcNow.AddDays(7));
		repository.Add(refreshToken);
		await unitOfWork.SaveChangesAsync(CancellationToken.None);

		refreshToken.Revoke();
		repository.Update(refreshToken);
		await unitOfWork.SaveChangesAsync(CancellationToken.None);

		var reloaded = await repository.GetByHashAsync(tokenHash, CancellationToken.None);

		Assert.NotNull(reloaded);
		Assert.NotNull(reloaded!.RevokedAt);
		Assert.NotNull(reloaded.UpdatedAt);
	}
}
