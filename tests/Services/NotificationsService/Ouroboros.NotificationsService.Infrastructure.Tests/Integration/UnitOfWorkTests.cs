using Ouroboros.NotificationsService.Domain;
using Ouroboros.NotificationsService.Infrastructure;

namespace Ouroboros.NotificationsService.Infrastructure.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(NotificationsDatabaseCollection.Name)]
public sealed class UnitOfWorkTests
{
	private readonly NotificationsDatabaseFixture _fixture;

	public UnitOfWorkTests(NotificationsDatabaseFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task SaveChangesAsync_persists_every_pending_command_in_a_single_transaction()
	{
		await using var session = _fixture.CreateSession();
		var repository = new EmailDeliveryRepository(session);
		var unitOfWork = new UnitOfWork(session);
		var first = NewDelivery();
		var second = NewDelivery();

		repository.Add(first);
		repository.Add(second);
		await unitOfWork.SaveChangesAsync(CancellationToken.None);

		var reloadedFirst = await repository.GetByRequestAsync("auth", first.RequestId, CancellationToken.None);
		var reloadedSecond = await repository.GetByRequestAsync("auth", second.RequestId, CancellationToken.None);

		Assert.NotNull(reloadedFirst);
		Assert.NotNull(reloadedSecond);
	}

	[Fact]
	public async Task SaveChangesAsync_rolls_back_every_pending_command_when_one_of_them_violates_a_constraint()
	{
		// A chave (producer, request_id) é única (ix_email_deliveries_producer_request_id). Duas
		// entregas com a mesma chave na mesma leva de SaveChangesAsync competem pela mesma transação:
		// uma delas viola a constraint, e isso precisa desfazer as duas — não só a que falhou.
		await using var session = _fixture.CreateSession();
		var repository = new EmailDeliveryRepository(session);
		var unitOfWork = new UnitOfWork(session);
		var requestId = Guid.NewGuid();
		var first = NewDelivery(requestId);
		var duplicate = NewDelivery(requestId);

		repository.Add(first);
		repository.Add(duplicate);

		await Assert.ThrowsAnyAsync<Exception>(() => unitOfWork.SaveChangesAsync(CancellationToken.None));

		var reloaded = await repository.GetByRequestAsync("auth", requestId, CancellationToken.None);

		Assert.Null(reloaded);
	}

	[Fact]
	public async Task A_failed_save_does_not_replay_its_commands_on_the_next_save()
	{
		// Mesma regressão coberta no UnitOfWork do Auth: comando enfileirado numa gravação que falhou
		// não pode ressuscitar numa gravação seguinte, bem-sucedida, na mesma sessão.
		await using var session = _fixture.CreateSession();
		var repository = new EmailDeliveryRepository(session);
		var unitOfWork = new UnitOfWork(session);
		var requestId = Guid.NewGuid();
		var first = NewDelivery(requestId);
		var duplicate = NewDelivery(requestId);
		repository.Add(first);
		repository.Add(duplicate);

		await Assert.ThrowsAnyAsync<Exception>(() => unitOfWork.SaveChangesAsync(CancellationToken.None));

		var afterward = NewDelivery();
		repository.Add(afterward);
		await unitOfWork.SaveChangesAsync(CancellationToken.None);

		var reloadedFirst = await repository.GetByRequestAsync("auth", requestId, CancellationToken.None);
		var reloadedAfterward = await repository.GetByRequestAsync("auth", afterward.RequestId, CancellationToken.None);

		Assert.Null(reloadedFirst);
		Assert.NotNull(reloadedAfterward);
	}

	private static EmailDelivery NewDelivery(Guid? requestId = null)
	{
		var unique = Guid.NewGuid().ToString("N");

		return new EmailDelivery(
			producer: "auth",
			requestId: requestId ?? Guid.NewGuid(),
			recipient: $"{unique}@example.com",
			templateKey: "user-creation-validation",
			templateVersion: 1,
			locale: "pt-BR",
			subject: "Assunto de teste",
			bodyHtml: "<p>Corpo de teste</p>",
			contentFingerprint: unique,
			expiresAt: DateTime.UtcNow.AddDays(1),
			correlationId: null
		);
	}
}
