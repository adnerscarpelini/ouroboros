using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.NotificationsService.Application;
using Ouroboros.NotificationsService.Domain;
using Ouroboros.NotificationsService.Infrastructure;

namespace Ouroboros.NotificationsService.Infrastructure.Tests;

public class EmailDeliveryDispatcherTests
{
	private static readonly EmailDeliveryOptions Options = new(
		PollingInterval: TimeSpan.FromSeconds(15),
		BatchSize: 20,
		MaxAttempts: 3,
		InitialRetryDelay: TimeSpan.FromSeconds(15),
		MaxRetryDelay: TimeSpan.FromMinutes(15)
	);

	private readonly InMemoryEmailDeliveryRepository _repository = new();
	private readonly FakeEmailSender _emailSender = new();
	private readonly FakeUnitOfWork _unitOfWork = new();

	private EmailDeliveryDispatcherService CreateDispatcher()
	{
		return new EmailDeliveryDispatcherService(
			emailDeliveryRepository: _repository,
			emailSender: _emailSender,
			unitOfWork: _unitOfWork,
			options: Options,
			logger: NullLogger<EmailDeliveryDispatcherService>.Instance
		);
	}

	private EmailDelivery AddDelivery(
		string recipient = "joao.silva@example.com",
		DateTime? expiresAt = null
	)
	{
		var delivery = new EmailDelivery(
			producer: "auth",
			requestId: Guid.NewGuid(),
			recipient: recipient,
			templateKey: "auth.email-confirmation",
			templateVersion: 1,
			locale: "pt-BR",
			subject: "Confirme seu cadastro",
			bodyHtml: "<p>Olá!</p>",
			contentFingerprint: "abc123",
			expiresAt: expiresAt ?? DateTime.UtcNow.AddHours(24),
			correlationId: "correlation-1"
		);

		_repository.Add(delivery);

		return delivery;
	}

	[Fact]
	public async Task DispatchDueAsync_SendsPendingDeliveryAndMarksItAccepted()
	{
		var delivery = AddDelivery();

		var processed = await CreateDispatcher().DispatchDueAsync(CancellationToken.None);

		Assert.Equal(1, processed);
		Assert.Equal(EmailDeliveryStatus.AcceptedByProvider, delivery.Status);
		Assert.Equal("joao.silva@example.com", Assert.Single(_emailSender.SentRecipients));
		Assert.Equal(1, _unitOfWork.SaveChangesCount);
	}

	[Fact]
	public async Task DispatchDueAsync_WithNothingDue_DoesNotTouchTheProvider()
	{
		var processed = await CreateDispatcher().DispatchDueAsync(CancellationToken.None);

		Assert.Equal(0, processed);
		Assert.Empty(_emailSender.SentRecipients);
		Assert.Equal(0, _unitOfWork.SaveChangesCount);
	}

	[Fact]
	public async Task DispatchDueAsync_WhenProviderFails_SchedulesRetryAndKeepsHistory()
	{
		var delivery = AddDelivery(recipient: "falha@example.com");
		_emailSender.FailingRecipient = "falha@example.com";

		await CreateDispatcher().DispatchDueAsync(CancellationToken.None);

		Assert.Equal(EmailDeliveryStatus.RetryScheduled, delivery.Status);
		Assert.Equal(1, delivery.AttemptCount);
		Assert.NotNull(delivery.NextAttemptAt);

		var attempt = Assert.Single(delivery.Attempts);
		Assert.False(attempt.Succeeded);
		Assert.Contains("servidor SMTP indisponivel", attempt.Error);
	}

	[Fact]
	public async Task DispatchDueAsync_AfterExhaustingAttempts_MarksDeliveryFailed()
	{
		var delivery = AddDelivery(recipient: "falha@example.com");
		_emailSender.FailingRecipient = "falha@example.com";

		var dispatcher = CreateDispatcher();

		for (var round = 0; round < Options.MaxAttempts; round++)
		{
			// A espera calculada é ignorada aqui de propósito: o que está sob teste é o orçamento de
			// tentativas, não o relógio.
			await dispatcher.DispatchDueAsync(CancellationToken.None);
			_repository.ClearRetrySchedule();
		}

		Assert.Equal(EmailDeliveryStatus.Failed, delivery.Status);
		Assert.Equal(Options.MaxAttempts, delivery.AttemptCount);
		Assert.Null(delivery.NextAttemptAt);
	}

	[Fact]
	public async Task DispatchDueAsync_WithExpiredDelivery_DoesNotSendIt()
	{
		var delivery = AddDelivery(expiresAt: DateTime.UtcNow.AddMinutes(-1));

		await CreateDispatcher().DispatchDueAsync(CancellationToken.None);

		// Entregar um link já vencido só produz frustração na caixa de entrada.
		Assert.Equal(EmailDeliveryStatus.Expired, delivery.Status);
		Assert.Empty(_emailSender.SentRecipients);
		Assert.Empty(delivery.Attempts);
	}

	[Fact]
	public async Task DispatchDueAsync_KeepsDeliveringTheBatchAfterOneFailure()
	{
		var failingDelivery = AddDelivery(recipient: "falha@example.com");
		var healthyDelivery = AddDelivery();
		_emailSender.FailingRecipient = "falha@example.com";

		var processed = await CreateDispatcher().DispatchDueAsync(CancellationToken.None);

		Assert.Equal(2, processed);
		Assert.Equal(EmailDeliveryStatus.RetryScheduled, failingDelivery.Status);
		Assert.Equal(EmailDeliveryStatus.AcceptedByProvider, healthyDelivery.Status);
	}
}
