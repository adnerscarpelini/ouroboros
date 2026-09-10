using Ouroboros.BuildingBlocks.Application;
using Ouroboros.Contracts.Notifications;
using Ouroboros.Services.Notifications.Application;

namespace Ouroboros.Services.Notifications.Application.Tests;

public class EmailDeliveryIntakeServiceTests
{
	private readonly FakeEmailDeliveryRepository _repository = new();
	private readonly FakeEmailTemplateRenderer _renderer = new();
	private readonly FakeUnitOfWork _unitOfWork = new();

	private EmailDeliveryIntakeService CreateService()
	{
		return new EmailDeliveryIntakeService(
			emailDeliveryRepository: _repository,
			emailTemplateRenderer: _renderer,
			unitOfWork: _unitOfWork
		);
	}

	private static MessageEnvelope CreateEnvelope(
		Guid? messageId = null,
		string producer = NotificationProducers.Auth,
		string messageType = NotificationMessageTypes.EmailRequested,
		int schemaVersion = NotificationMessageTypes.EmailRequestedSchemaVersion
	)
	{
		return new MessageEnvelope(
			MessageId: messageId ?? Guid.NewGuid(),
			Producer: producer,
			MessageType: messageType,
			SchemaVersion: schemaVersion,
			OccurredAt: DateTime.UtcNow,
			CorrelationId: "correlation-1",
			Payload: "{}"
		);
	}

	private static EmailNotificationRequestedV1 CreateRequest(
		string recipient = "joao.silva@example.com",
		string templateKey = EmailTemplateKeys.AuthEmailConfirmation,
		string confirmationUrl = "http://localhost:5082/api/auth/confirm-email?token=abc"
	)
	{
		return new EmailNotificationRequestedV1(
			Recipient: recipient,
			TemplateKey: templateKey,
			TemplateVersion: 1,
			Locale: "pt-BR",
			Data: new Dictionary<string, string>
			{
				["FullName"] = "João Silva",
				["ConfirmationUrl"] = confirmationUrl
			},
			ExpiresAt: DateTime.UtcNow.AddHours(24)
		);
	}

	[Fact]
	public async Task AcceptAsync_WithValidRequest_PersistsDeliveryRenderedOnce()
	{
		var envelope = CreateEnvelope();

		var result = await CreateService().AcceptAsync(
			envelope: envelope,
			request: CreateRequest(),
			cancellationToken: CancellationToken.None
		);

		Assert.Equal(EmailIntakeOutcome.Accepted, result.Outcome);

		var delivery = Assert.Single(_repository.Deliveries);
		Assert.Equal(envelope.MessageId, delivery.RequestId);
		Assert.Equal(NotificationProducers.Auth, delivery.Producer);
		Assert.Equal("Confirme seu cadastro", delivery.Subject);
		Assert.Equal("correlation-1", delivery.CorrelationId);
		// O conteúdo é montado uma vez, na aceitação: duas tentativas da mesma entrega não podem
		// produzir e-mails diferentes.
		Assert.Equal(1, _renderer.RenderCount);
		Assert.Equal(1, _unitOfWork.SaveChangesCount);
	}

	[Fact]
	public async Task AcceptAsync_WithSameRequestTwice_DoesNotCreateASecondDelivery()
	{
		var envelope = CreateEnvelope();
		var request = CreateRequest();
		var service = CreateService();

		await service.AcceptAsync(envelope, request, CancellationToken.None);

		var result = await service.AcceptAsync(envelope, request, CancellationToken.None);

		// Reentrega é o comportamento normal de um transporte at-least-once, não erro: confirmar o
		// consumo sem enviar um segundo e-mail é exatamente o que se espera.
		Assert.Equal(EmailIntakeOutcome.AlreadyAccepted, result.Outcome);
		Assert.Single(_repository.Deliveries);
		Assert.Equal(1, _renderer.RenderCount);
	}

	[Fact]
	public async Task AcceptAsync_WithSameRequestIdButDifferentContent_ReportsConflict()
	{
		var envelope = CreateEnvelope();
		var service = CreateService();

		await service.AcceptAsync(envelope, CreateRequest(), CancellationToken.None);

		var result = await service.AcceptAsync(
			envelope: envelope,
			request: CreateRequest(confirmationUrl: "http://localhost:5082/api/auth/confirm-email?token=outro"),
			cancellationToken: CancellationToken.None
		);

		// Nunca sobrescrever uma entrega existente: mesma chave com conteúdo diferente é conflito.
		Assert.Equal(EmailIntakeOutcome.Conflict, result.Outcome);
		Assert.Single(_repository.Deliveries);
	}

	[Fact]
	public async Task AcceptAsync_WithUnknownSchemaVersion_IsRejected()
	{
		var result = await CreateService().AcceptAsync(
			envelope: CreateEnvelope(schemaVersion: 99),
			request: CreateRequest(),
			cancellationToken: CancellationToken.None
		);

		Assert.Equal(EmailIntakeOutcome.Rejected, result.Outcome);
		Assert.Empty(_repository.Deliveries);
	}

	[Fact]
	public async Task AcceptAsync_WithUnknownMessageType_IsRejected()
	{
		var result = await CreateService().AcceptAsync(
			envelope: CreateEnvelope(messageType: "notifications.sms.requested"),
			request: CreateRequest(),
			cancellationToken: CancellationToken.None
		);

		Assert.Equal(EmailIntakeOutcome.Rejected, result.Outcome);
		Assert.Empty(_repository.Deliveries);
	}

	[Fact]
	public async Task AcceptAsync_WithTemplateOutsideTheCatalog_IsRejected()
	{
		var result = await CreateService().AcceptAsync(
			envelope: CreateEnvelope(),
			request: CreateRequest(templateKey: "auth.qualquer-coisa"),
			cancellationToken: CancellationToken.None
		);

		// Template é escolhido por chave conhecida, nunca por caminho vindo na mensagem.
		Assert.Equal(EmailIntakeOutcome.Rejected, result.Outcome);
		Assert.Empty(_repository.Deliveries);
	}

	[Fact]
	public async Task AcceptAsync_WithProducerThatDoesNotOwnTheTemplate_IsRejected()
	{
		var result = await CreateService().AcceptAsync(
			envelope: CreateEnvelope(producer: "vendas"),
			request: CreateRequest(),
			cancellationToken: CancellationToken.None
		);

		// Um produtor não dispara o e-mail de outro.
		Assert.Equal(EmailIntakeOutcome.Rejected, result.Outcome);
		Assert.Empty(_repository.Deliveries);
	}

	[Fact]
	public async Task AcceptAsync_WithInvalidRecipient_IsRejected()
	{
		var result = await CreateService().AcceptAsync(
			envelope: CreateEnvelope(),
			request: CreateRequest(recipient: "nao-e-um-email"),
			cancellationToken: CancellationToken.None
		);

		Assert.Equal(EmailIntakeOutcome.Rejected, result.Outcome);
		Assert.Empty(_repository.Deliveries);
	}

	[Fact]
	public async Task AcceptAsync_WithMissingRequiredPlaceholder_IsRejected()
	{
		var request = new EmailNotificationRequestedV1(
			Recipient: "joao.silva@example.com",
			TemplateKey: EmailTemplateKeys.AuthEmailConfirmation,
			TemplateVersion: 1,
			Locale: "pt-BR",
			Data: new Dictionary<string, string> { ["FullName"] = "João Silva" },
			ExpiresAt: DateTime.UtcNow.AddHours(24)
		);

		var result = await CreateService().AcceptAsync(
			envelope: CreateEnvelope(),
			request: request,
			cancellationToken: CancellationToken.None
		);

		Assert.Equal(EmailIntakeOutcome.Rejected, result.Outcome);
		Assert.Empty(_repository.Deliveries);
	}
}
