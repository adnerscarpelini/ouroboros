using Ouroboros.BuildingBlocks.Domain;

namespace Ouroboros.NotificationsService.Domain;

// Uma entrega de e-mail aceita a partir de uma solicitação de um serviço produtor. A chave
// (Producer, RequestId) é única: é ela que absorve a reentrega inevitável de um transporte
// at-least-once — a mesma solicitação chegando duas vezes não vira dois e-mails.
//
// Esta tabela também cumpre o papel de inbox das solicitações válidas; não existe uma inbox separada.
public sealed class EmailDelivery : Entity
{
	private const int MaxLastErrorLength = 500;

	private readonly List<EmailDeliveryAttempt> _attempts = [];

	// Metade produtora da chave de deduplicação (ex.: "auth").
	public string Producer { get; private set; } = null!;
	// O MessageId do envelope, estável entre republicações do mesmo pedido.
	public Guid RequestId { get; private set; }
	public string Recipient { get; private set; } = null!;
	public string TemplateKey { get; private set; } = null!;
	public int TemplateVersion { get; private set; }
	public string Locale { get; private set; } = null!;
	// Assunto e corpo são renderizados uma única vez, na aceitação, e reusados em toda tentativa:
	// duas tentativas da mesma entrega não podem produzir conteúdos diferentes.
	public string Subject { get; private set; } = null!;
	public string BodyHtml { get; private set; } = null!;
	// Hash do conteúdo imutável da solicitação. Mesma chave com conteúdo diferente é conflito, não
	// atualização — nunca sobrescrever uma entrega existente.
	public string ContentFingerprint { get; private set; } = null!;
	public DateTime ExpiresAt { get; private set; }
	public string? CorrelationId { get; private set; }
	public EmailDeliveryStatus Status { get; private set; }
	public int AttemptCount { get; private set; }
	public DateTime? NextAttemptAt { get; private set; }
	public DateTime? SentAt { get; private set; }
	public string? LastError { get; private set; }

	public IReadOnlyCollection<EmailDeliveryAttempt> Attempts => _attempts;

	public static EmailDelivery Rehydrate(long id, Guid externalId, DateTime createdAt, DateTime? updatedAt, string producer, Guid requestId, string recipient, string templateKey, int templateVersion, string locale, string subject, string bodyHtml, string contentFingerprint, DateTime expiresAt, string? correlationId, EmailDeliveryStatus status, int attemptCount, DateTime? nextAttemptAt, DateTime? sentAt, string? lastError)
	{
		var item = new EmailDelivery(
			producer,
			requestId,
			recipient,
			templateKey,
			templateVersion,
			locale,
			subject,
			bodyHtml,
			contentFingerprint,
			expiresAt,
			correlationId
		)
		{
			Status = status,
			AttemptCount = attemptCount,
			NextAttemptAt = nextAttemptAt,
			SentAt = sentAt,
			LastError = lastError
		};

		item.RestorePersistence(id, externalId, createdAt, updatedAt);

		return item;
	}

	// Construtor sem parâmetros usado exclusivamente pela fábrica de reidratação SQL.
	private EmailDelivery()
	{
	}

	public EmailDelivery(
		string producer,
		Guid requestId,
		string recipient,
		string templateKey,
		int templateVersion,
		string locale,
		string subject,
		string bodyHtml,
		string contentFingerprint,
		DateTime expiresAt,
		string? correlationId
	)
	{
		Producer = producer;
		RequestId = requestId;
		Recipient = recipient;
		TemplateKey = templateKey;
		TemplateVersion = templateVersion;
		Locale = locale;
		Subject = subject;
		BodyHtml = bodyHtml;
		ContentFingerprint = contentFingerprint;
		ExpiresAt = expiresAt;
		CorrelationId = correlationId;
		Status = EmailDeliveryStatus.Pending;
		AttemptCount = 0;
	}

	// Registrada antes da chamada ao provedor: uma queda no meio do envio precisa deixar rastro de que
	// a tentativa começou, mesmo sem resultado conhecido.
	public EmailDeliveryAttempt StartAttempt()
	{
		AttemptCount++;

		var attempt = new EmailDeliveryAttempt(
			delivery: this,
			attemptNumber: AttemptCount
		);

		_attempts.Add(attempt);

		return attempt;
	}

	public void MarkAsAccepted(EmailDeliveryAttempt attempt)
	{
		attempt.CompleteAsSucceeded();

		Status = EmailDeliveryStatus.AcceptedByProvider;
		SentAt = DateTime.UtcNow;
		NextAttemptAt = null;
		LastError = null;
	}

	public void ScheduleRetry(
		EmailDeliveryAttempt attempt,
		string error,
		DateTime nextAttemptAt
	)
	{
		var truncatedError = Truncate(error);

		attempt.CompleteAsFailed(truncatedError);

		Status = EmailDeliveryStatus.RetryScheduled;
		NextAttemptAt = nextAttemptAt;
		LastError = truncatedError;
	}

	public void MarkAsFailed(
		EmailDeliveryAttempt attempt,
		string error
	)
	{
		var truncatedError = Truncate(error);

		attempt.CompleteAsFailed(truncatedError);

		Status = EmailDeliveryStatus.Failed;
		NextAttemptAt = null;
		LastError = truncatedError;
	}

	public void MarkAsExpired()
	{
		Status = EmailDeliveryStatus.Expired;
		NextAttemptAt = null;
	}

	public bool IsDueAt(DateTime instant)
	{
		return (Status == EmailDeliveryStatus.Pending || Status == EmailDeliveryStatus.RetryScheduled)
			&& (NextAttemptAt is null || NextAttemptAt <= instant);
	}

	public bool IsExpiredAt(DateTime instant)
	{
		return ExpiresAt <= instant;
	}

	public bool HasExhaustedAttempts(int maxAttempts)
	{
		return AttemptCount >= maxAttempts;
	}

	private static string Truncate(string error)
	{
		return error.Length > MaxLastErrorLength
			? error[..MaxLastErrorLength]
			: error;
	}
}
