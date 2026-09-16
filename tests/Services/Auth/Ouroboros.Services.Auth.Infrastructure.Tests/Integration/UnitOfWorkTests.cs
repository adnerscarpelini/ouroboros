using Ouroboros.Services.Auth.Infrastructure;

namespace Ouroboros.Services.Auth.Infrastructure.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(AuthDatabaseCollection.Name)]
public sealed class UnitOfWorkTests
{
	private readonly AuthDatabaseFixture _fixture;

	public UnitOfWorkTests(AuthDatabaseFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ExecuteInTransactionAsync_rolls_back_everything_when_an_operation_fails()
	{
		await using var session = _fixture.CreateSession();
		var repository = new UserRepository(session);
		var unitOfWork = new UnitOfWork(session);
		var user = AuthTestData.NewUser();

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			unitOfWork.ExecuteInTransactionAsync(
				_ =>
				{
					repository.Add(user);
					throw new InvalidOperationException("Falha simulada no meio do caso de uso.");
				},
				CancellationToken.None));

		var persisted = await repository.GetByLoginAsync(user.Login, CancellationToken.None);

		Assert.Null(persisted);
	}

	[Fact]
	public async Task ExecuteInTransactionAsync_persists_everything_when_the_operation_succeeds()
	{
		await using var session = _fixture.CreateSession();
		var repository = new UserRepository(session);
		var unitOfWork = new UnitOfWork(session);
		var user = AuthTestData.NewUser();

		await unitOfWork.ExecuteInTransactionAsync(
			_ =>
			{
				repository.Add(user);
				return Task.CompletedTask;
			},
			CancellationToken.None);

		var persisted = await repository.GetByLoginAsync(user.Login, CancellationToken.None);

		Assert.NotNull(persisted);
	}

	[Fact]
	public async Task A_failed_transaction_does_not_replay_its_commands_on_the_next_save()
	{
		// Regressão: DbSession costumava manter comandos enfileirados na fila mesmo depois de um
		// rollback. Uma segunda operação bem-sucedida na mesma sessão não pode ressuscitar o
		// usuário da tentativa anterior, que falhou de propósito.
		await using var session = _fixture.CreateSession();
		var repository = new UserRepository(session);
		var unitOfWork = new UnitOfWork(session);
		var failedUser = AuthTestData.NewUser();
		var successfulUser = AuthTestData.NewUser();

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			unitOfWork.ExecuteInTransactionAsync(
				_ =>
				{
					repository.Add(failedUser);
					throw new InvalidOperationException("Falha simulada.");
				},
				CancellationToken.None));

		repository.Add(successfulUser);
		await unitOfWork.SaveChangesAsync(CancellationToken.None);

		var reloadedFailedUser = await repository.GetByLoginAsync(failedUser.Login, CancellationToken.None);
		var reloadedSuccessfulUser = await repository.GetByLoginAsync(successfulUser.Login, CancellationToken.None);

		Assert.Null(reloadedFailedUser);
		Assert.NotNull(reloadedSuccessfulUser);
	}
}
