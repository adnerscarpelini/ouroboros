using Ouroboros.NotificationsService.Domain;

namespace Ouroboros.NotificationsService.Domain.Tests;

public class EmailDeliveryTests
{
	private static EmailDelivery CreateDelivery(DateTime? expiresAt = null)
	{
		return new EmailDelivery(
			producer: "auth",
			requestId: Guid.NewGuid(),
			recipient: "joao.silva@example.com",
			templateKey: "auth.email-confirmation",
			templateVersion: 1,
			locale: "pt-BR",
			subject: "Confirme seu cadastro",
			bodyHtml: "<p>Olá!</p>",
			contentFingerprint: "abc123",
			expiresAt: expiresAt ?? DateTime.UtcNow.AddHours(24),
			correlationId: "correlation-1"
		);
	}

	[Fact]
	public void Constructor_StartsPendingAndDue()
	{
		var delivery = CreateDelivery();

		Assert.Equal(EmailDeliveryStatus.Pending, delivery.Status);
		Assert.Equal(0, delivery.AttemptCount);
		Assert.Empty(delivery.Attempts);
		Assert.True(delivery.IsDueAt(DateTime.UtcNow));
	}

	[Fact]
	public void StartAttempt_RecordsAttemptBeforeAnyResultIsKnown()
	{
		var delivery = CreateDelivery();

		var attempt = delivery.StartAttempt();

		// A tentativa nasce sem resultado: se o processo cair no meio do envio, fica o rastro de que
		// ela começou. É o caso ambíguo do SMTP, e ele precisa ser visível.
		Assert.Equal(1, attempt.AttemptNumber);
		Assert.Null(attempt.CompletedAt);
		Assert.Null(attempt.Succeeded);
		Assert.Equal(1, delivery.AttemptCount);
		Assert.Single(delivery.Attempts);
	}

	[Fact]
	public void MarkAsAccepted_StopsBeingDue()
	{
		var delivery = CreateDelivery();
		var attempt = delivery.StartAttempt();

		delivery.MarkAsAccepted(attempt);

		Assert.Equal(EmailDeliveryStatus.AcceptedByProvider, delivery.Status);
		Assert.NotNull(delivery.SentAt);
		Assert.True(attempt.Succeeded);
		Assert.False(delivery.IsDueAt(DateTime.UtcNow.AddYears(1)));
	}

	[Fact]
	public void ScheduleRetry_KeepsDeliveryDueOnlyAfterTheScheduledInstant()
	{
		var delivery = CreateDelivery();
		var attempt = delivery.StartAttempt();
		var nextAttemptAt = DateTime.UtcNow.AddMinutes(5);

		delivery.ScheduleRetry(
			attempt: attempt,
			error: "smtp indisponivel",
			nextAttemptAt: nextAttemptAt
		);

		Assert.Equal(EmailDeliveryStatus.RetryScheduled, delivery.Status);
		Assert.False(attempt.Succeeded);
		Assert.Equal("smtp indisponivel", attempt.Error);
		Assert.False(delivery.IsDueAt(DateTime.UtcNow));
		Assert.True(delivery.IsDueAt(nextAttemptAt));
	}

	[Fact]
	public void MarkAsFailed_StopsBeingDue()
	{
		var delivery = CreateDelivery();
		var attempt = delivery.StartAttempt();

		delivery.MarkAsFailed(
			attempt: attempt,
			error: "destinatario invalido"
		);

		Assert.Equal(EmailDeliveryStatus.Failed, delivery.Status);
		Assert.Equal("destinatario invalido", delivery.LastError);
		Assert.False(delivery.IsDueAt(DateTime.UtcNow.AddYears(1)));
	}

	[Fact]
	public void ScheduleRetry_TruncatesLongError()
	{
		var delivery = CreateDelivery();
		var attempt = delivery.StartAttempt();

		delivery.ScheduleRetry(
			attempt: attempt,
			error: new string('x', 1000),
			nextAttemptAt: DateTime.UtcNow
		);

		Assert.Equal(500, delivery.LastError!.Length);
	}

	[Fact]
	public void IsExpiredAt_IsTrueOnceTheLinkHasDied()
	{
		var delivery = CreateDelivery(expiresAt: DateTime.UtcNow.AddMinutes(-1));

		Assert.True(delivery.IsExpiredAt(DateTime.UtcNow));
	}

	[Fact]
	public void MarkAsExpired_StopsBeingDue()
	{
		var delivery = CreateDelivery(expiresAt: DateTime.UtcNow.AddMinutes(-1));

		delivery.MarkAsExpired();

		Assert.Equal(EmailDeliveryStatus.Expired, delivery.Status);
		Assert.False(delivery.IsDueAt(DateTime.UtcNow));
	}

	[Fact]
	public void HasExhaustedAttempts_IsTrueOnceTheBudgetIsSpent()
	{
		var delivery = CreateDelivery();

		delivery.StartAttempt();
		Assert.False(delivery.HasExhaustedAttempts(2));

		delivery.StartAttempt();
		Assert.True(delivery.HasExhaustedAttempts(2));
	}
}
