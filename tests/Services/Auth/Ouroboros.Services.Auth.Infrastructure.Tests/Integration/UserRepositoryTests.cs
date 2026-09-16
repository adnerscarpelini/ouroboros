using Ouroboros.Services.Auth.Infrastructure;

namespace Ouroboros.Services.Auth.Infrastructure.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(AuthDatabaseCollection.Name)]
public sealed class UserRepositoryTests
{
	private readonly AuthDatabaseFixture _fixture;

	public UserRepositoryTests(AuthDatabaseFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Add_persists_user_and_makes_it_retrievable_by_login_and_email()
	{
		await using var session = _fixture.CreateSession();
		var repository = new UserRepository(session);
		var unitOfWork = new UnitOfWork(session);
		var user = AuthTestData.NewUser();

		repository.Add(user);
		await unitOfWork.SaveChangesAsync(CancellationToken.None);

		var byLogin = await repository.GetByLoginAsync(user.Login, CancellationToken.None);
		var byEmail = await repository.GetByEmailAsync(user.Email, CancellationToken.None);

		Assert.NotNull(byLogin);
		Assert.NotNull(byEmail);
		Assert.Equal(user.ExternalId, byLogin!.ExternalId);
		Assert.Equal(user.ExternalId, byEmail!.ExternalId);
		Assert.True(byLogin.Id > 0);
		Assert.Null(byLogin.UpdatedAt);
	}

	[Fact]
	public async Task ExistsByLoginAsync_and_ExistsByEmailAsync_reflect_persisted_state()
	{
		await using var session = _fixture.CreateSession();
		var repository = new UserRepository(session);
		var unitOfWork = new UnitOfWork(session);
		var user = AuthTestData.NewUser();

		var existedBefore = await repository.ExistsByLoginAsync(user.Login, CancellationToken.None);

		repository.Add(user);
		await unitOfWork.SaveChangesAsync(CancellationToken.None);

		var existedAfterByLogin = await repository.ExistsByLoginAsync(user.Login, CancellationToken.None);
		var existedAfterByEmail = await repository.ExistsByEmailAsync(user.Email, CancellationToken.None);

		Assert.False(existedBefore);
		Assert.True(existedAfterByLogin);
		Assert.True(existedAfterByEmail);
	}

	[Fact]
	public async Task GetByLoginAsync_returns_null_when_user_does_not_exist()
	{
		await using var session = _fixture.CreateSession();
		var repository = new UserRepository(session);

		var result = await repository.GetByLoginAsync(
			$"login-inexistente-{Guid.NewGuid():N}",
			CancellationToken.None);

		Assert.Null(result);
	}

	[Fact]
	public async Task Update_persists_changes_and_stamps_updated_at()
	{
		await using var session = _fixture.CreateSession();
		var repository = new UserRepository(session);
		var unitOfWork = new UnitOfWork(session);
		var user = AuthTestData.NewUser();
		repository.Add(user);
		await unitOfWork.SaveChangesAsync(CancellationToken.None);

		user.RegisterFailedLoginAttempt();
		repository.Update(user);
		await unitOfWork.SaveChangesAsync(CancellationToken.None);

		var reloaded = await repository.GetByLoginAsync(user.Login, CancellationToken.None);

		Assert.NotNull(reloaded);
		Assert.Equal(1, reloaded!.FailedLoginAttempts);
		Assert.NotNull(reloaded.UpdatedAt);
	}
}
