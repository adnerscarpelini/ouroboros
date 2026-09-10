using System.Globalization;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Ouroboros.BuildingBlocks.Application;
using Ouroboros.Contracts.Notifications;
using Ouroboros.Services.Notifications.Domain;

namespace Ouroboros.Services.Notifications.Application;

public sealed class EmailDeliveryIntakeService : IEmailDeliveryIntakeService
{
	private readonly IEmailDeliveryRepository _emailDeliveryRepository;
	private readonly IEmailTemplateRenderer _emailTemplateRenderer;
	private readonly IUnitOfWork _unitOfWork;

	public EmailDeliveryIntakeService(
		IEmailDeliveryRepository emailDeliveryRepository,
		IEmailTemplateRenderer emailTemplateRenderer,
		IUnitOfWork unitOfWork
	)
	{
		_emailDeliveryRepository = emailDeliveryRepository;
		_emailTemplateRenderer = emailTemplateRenderer;
		_unitOfWork = unitOfWork;
	}

	public async Task<EmailIntakeResult> AcceptAsync(
		MessageEnvelope envelope,
		EmailNotificationRequestedV1 request,
		CancellationToken cancellationToken
	)
	{
		if (envelope.MessageType != NotificationMessageTypes.EmailRequested)
		{
			return EmailIntakeResult.Rejected($"Tipo de mensagem desconhecido: '{envelope.MessageType}'.");
		}

		if (envelope.SchemaVersion != NotificationMessageTypes.EmailRequestedSchemaVersion)
		{
			return EmailIntakeResult.Rejected($"Versão de contrato desconhecida: {envelope.SchemaVersion}.");
		}

		var template = EmailTemplateCatalog.Find(
			key: request.TemplateKey,
			version: request.TemplateVersion,
			locale: request.Locale
		);

		if (template is null)
		{
			return EmailIntakeResult.Rejected($"Template '{request.TemplateKey}' v{request.TemplateVersion} ({request.Locale}) não está no catálogo.");
		}

		// O produtor declarado no envelope não basta por si só — quem publica é validado também pela
		// credencial do transporte —, mas um produtor não pode disparar o template de outro.
		if (template.Producer != envelope.Producer)
		{
			return EmailIntakeResult.Rejected($"Produtor '{envelope.Producer}' não tem permissão sobre o template '{template.Key}'.");
		}

		if (!MailAddress.TryCreate(request.Recipient, out _))
		{
			return EmailIntakeResult.Rejected("Destinatário inválido.");
		}

		var missingPlaceholders = template.RequiredPlaceholders
			.Where(placeholder => !request.Data.TryGetValue(placeholder, out var value) || string.IsNullOrWhiteSpace(value))
			.ToList();

		if (missingPlaceholders.Count > 0)
		{
			return EmailIntakeResult.Rejected($"Dados obrigatórios ausentes para o template '{template.Key}': {string.Join(", ", missingPlaceholders)}.");
		}

		var contentFingerprint = CalculateContentFingerprint(request);

		var existingDelivery = await _emailDeliveryRepository.GetByRequestAsync(
			producer: envelope.Producer,
			requestId: envelope.MessageId,
			cancellationToken: cancellationToken
		);

		if (existingDelivery is not null)
		{
			// Reentrega: o transporte é at-least-once, então a mesma solicitação chegar de novo é
			// esperado, não erro. Conteúdo diferente sob a mesma chave é outra história.
			return existingDelivery.ContentFingerprint == contentFingerprint
				? EmailIntakeResult.AlreadyAccepted()
				: EmailIntakeResult.Conflict("Mesma solicitação com conteúdo diferente do já aceito.");
		}

		var renderedEmail = await _emailTemplateRenderer.RenderAsync(
			template: template,
			data: request.Data,
			cancellationToken: cancellationToken
		);

		_emailDeliveryRepository.Add(new EmailDelivery(
			producer: envelope.Producer,
			requestId: envelope.MessageId,
			recipient: request.Recipient,
			templateKey: template.Key,
			templateVersion: template.Version,
			locale: template.Locale,
			subject: renderedEmail.Subject,
			bodyHtml: renderedEmail.BodyHtml,
			contentFingerprint: contentFingerprint,
			expiresAt: request.ExpiresAt,
			correlationId: envelope.CorrelationId
		));

		await _unitOfWork.SaveChangesAsync(cancellationToken);

		return EmailIntakeResult.Accepted();
	}

	// Impressão digital do conteúdo imutável da solicitação. Serve só para detectar duas solicitações
	// diferentes usando a mesma chave — não é chave de deduplicação (essa é produtor + solicitação) e
	// não substitui a idempotência: dois pedidos legítimos de recuperação de senha têm conteúdo
	// distinto e devem gerar dois e-mails.
	private static string CalculateContentFingerprint(EmailNotificationRequestedV1 request)
	{
		var canonicalContent = new StringBuilder()
			.Append(request.Recipient).Append('\n')
			.Append(request.TemplateKey).Append('\n')
			.Append(request.TemplateVersion).Append('\n')
			.Append(request.Locale).Append('\n')
			.Append(request.ExpiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)).Append('\n');

		foreach (var entry in request.Data.OrderBy(entry => entry.Key, StringComparer.Ordinal))
		{
			canonicalContent.Append(entry.Key).Append('=').Append(entry.Value).Append('\n');
		}

		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalContent.ToString()));

		return Convert.ToHexStringLower(hash);
	}
}
