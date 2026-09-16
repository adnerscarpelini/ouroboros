using Ouroboros.NotificationsService.Domain;
using Ouroboros.NotificationsService.Infrastructure;

namespace Ouroboros.NotificationsService.Infrastructure.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(NotificationsDatabaseCollection.Name)]
public sealed class EmailDeliveryRepositoryTests
{
	private readonly NotificationsDatabaseFixture _fixture;

	public EmailDeliveryRepositoryTests(NotificationsDatabaseFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Add_persists_delivery_and_makes_it_retrievable_by_producer_and_request_id()
	{
		await using var session = _fixture.CreateSession();
		var repository = new EmailDeliveryRepository(session);
		var unitOfWork = new UnitOfWork(session);
		var requestId = Guid.NewGuid();
		var delivery = NewDelivery(requestId);

		repository.Add(delivery);
		await unitOfWork.SaveChangesAsync(CancellationToken.None);

		var reloaded = await repository.GetByRequestAsync("auth", requestId, CancellationToken.None);

		Assert.NotNull(reloaded);
		Assert.Equal(delivery.ExternalId, reloaded!.ExternalId);
		Assert.Equal(EmailDeliveryStatus.Pending, reloaded.Status);
		Assert.Equal(0, reloaded.AttemptCount);
	}

	[Fact]
	public async Task GetByRequestAsync_returns_null_when_the_request_does_not_exist()
	{
		await using var session = _fixture.CreateSession();
		var repository = new EmailDeliveryRepository(session);

		var reloaded = await repository.GetByRequestAsync("auth", Guid.NewGuid(), CancellationToken.None);

		Assert.Null(reloaded);
	}

	[Fact]
	public async Task Update_persists_status_transitions_and_stamps_updated_at()
	{
		await using var session = _fixture.CreateSession();
		var repository = new EmailDeliveryRepository(session);
		var unitOfWork = new UnitOfWork(session);
		var requestId = Guid.NewGuid();
		var delivery = NewDelivery(requestId);
		repository.Add(delivery);
		await unitOfWork.SaveChangesAsync(CancellationToken.None);

		var attempt = delivery.StartAttempt();
		delivery.MarkAsAccepted(attempt);
		repository.Update(delivery);
		await unitOfWork.SaveChangesAsync(CancellationToken.None);

		var reloaded = await repository.GetByRequestAsync("auth", requestId, CancellationToken.None);

		Assert.NotNull(reloaded);
		Assert.Equal(EmailDeliveryStatus.AcceptedByProvider, reloaded!.Status);
		Assert.Equal(1, reloaded.AttemptCount);
		Assert.NotNull(reloaded.SentAt);
		Assert.NotNull(reloaded.UpdatedAt);
	}

	[Fact]
	public async Task GetDueForDeliveryAsync_only_returns_pending_or_retry_scheduled_deliveries_that_are_due()
	{
		await using var session = _fixture.CreateSession();
		var repository = new EmailDeliveryRepository(session);
		var unitOfWork = new UnitOfWork(session);
		var now = DateTime.UtcNow;

		var duePending = NewDelivery(Guid.NewGuid());
		var notYetDue = NewDelivery(Guid.NewGuid());
		var accepted = NewDelivery(Guid.NewGuid());
		repository.Add(duePending);
		repository.Add(notYetDue);
		repository.Add(accepted);
		await unitOfWork.SaveChangesAsync(CancellationToken.None);

		var notYetDueAttempt = notYetDue.StartAttempt();
		notYetDue.ScheduleRetry(notYetDueAttempt, "falha transitória", now.AddHours(1));
		repository.Update(notYetDue);

		var acceptedAttempt = accepted.StartAttempt();
		accepted.MarkAsAccepted(acceptedAttempt);
		repository.Update(accepted);

		await unitOfWork.SaveChangesAsync(CancellationToken.None);

		var due = await repository.GetDueForDeliveryAsync(now, batchSize: 100, CancellationToken.None);

		var dueExternalIds = due.Select(item => item.ExternalId).ToArray();
		Assert.Contains(duePending.ExternalId, dueExternalIds);
		Assert.DoesNotContain(notYetDue.ExternalId, dueExternalIds);
		Assert.DoesNotContain(accepted.ExternalId, dueExternalIds);
	}

	private static EmailDelivery NewDelivery(Guid requestId)
	{
		var unique = Guid.NewGuid().ToString("N");

		return new EmailDelivery(
			producer: "auth",
			requestId: requestId,
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
