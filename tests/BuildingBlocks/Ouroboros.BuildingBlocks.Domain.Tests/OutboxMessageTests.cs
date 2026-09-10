using Ouroboros.BuildingBlocks.Domain;

namespace Ouroboros.BuildingBlocks.Domain.Tests;

public class OutboxMessageTests
{
	private static OutboxMessage CreateOutboxMessage()
	{
		return new OutboxMessage(
			producer: "auth",
			messageType: "notifications.email.requested",
			schemaVersion: 1,
			payload: "{}",
			correlationId: "correlation-1"
		);
	}

	[Fact]
	public void Constructor_StartsPendingWithMessageIdAlreadyGenerated()
	{
		var outboxMessage = CreateOutboxMessage();

		Assert.Equal(OutboxMessageStatus.Pending, outboxMessage.Status);
		Assert.Equal(0, outboxMessage.AttemptCount);
		Assert.Null(outboxMessage.PublishedAt);
		Assert.Null(outboxMessage.NextAttemptAt);
		// O produtor precisa do identificador antes do commit, para correlacionar sem ir ao banco.
		Assert.NotEqual(Guid.Empty, outboxMessage.ExternalId);
	}

	[Fact]
	public void IsDueAt_IsTrueWhileNoRetryHasBeenScheduled()
	{
		var outboxMessage = CreateOutboxMessage();

		Assert.True(outboxMessage.IsDueAt(DateTime.UtcNow));
	}

	[Fact]
	public void MarkAsPublished_StopsBeingDue()
	{
		var outboxMessage = CreateOutboxMessage();

		outboxMessage.MarkAsPublished();

		Assert.Equal(OutboxMessageStatus.Published, outboxMessage.Status);
		Assert.Equal(1, outboxMessage.AttemptCount);
		Assert.NotNull(outboxMessage.PublishedAt);
		Assert.False(outboxMessage.IsDueAt(DateTime.UtcNow.AddYears(1)));
	}

	[Fact]
	public void RegisterFailedAttempt_KeepsMessagePendingUntilNextAttempt()
	{
		var outboxMessage = CreateOutboxMessage();
		var nextAttemptAt = DateTime.UtcNow.AddMinutes(5);

		outboxMessage.RegisterFailedAttempt(
			error: "broker indisponivel",
			nextAttemptAt: nextAttemptAt
		);

		// Sem descarte por indisponibilidade: a mensagem continua pendente, só que mais tarde.
		Assert.Equal(OutboxMessageStatus.Pending, outboxMessage.Status);
		Assert.Equal(1, outboxMessage.AttemptCount);
		Assert.Equal("broker indisponivel", outboxMessage.LastError);
		Assert.False(outboxMessage.IsDueAt(DateTime.UtcNow));
		Assert.True(outboxMessage.IsDueAt(nextAttemptAt));
	}

	[Fact]
	public void RegisterFailedAttempt_TruncatesLongError()
	{
		var outboxMessage = CreateOutboxMessage();

		outboxMessage.RegisterFailedAttempt(
			error: new string('x', 1000),
			nextAttemptAt: DateTime.UtcNow
		);

		Assert.Equal(500, outboxMessage.LastError!.Length);
	}
}
