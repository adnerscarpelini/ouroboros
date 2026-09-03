namespace Ouroboros.BuildingBlocks.Domain;

public sealed class EmailMessage : Entity
{
	// O erro é guardado só para diagnóstico; truncar evita que um stack trace enorme
	// vindo do servidor SMTP inche a tabela.
	private const int MaxLastErrorLength = 500;

	public string Subject { get; private set; } = null!;
	public string BodyHtml { get; private set; } = null!;
	public string Recipient { get; private set; } = null!;
	public bool Sent { get; private set; }
	public DateTime? SentAt { get; private set; }
	public int AttemptCount { get; private set; }
	public DateTime? LastAttemptAt { get; private set; }
	public string? LastError { get; private set; }

	// Construtor sem parâmetros exclusivo para o EF Core materializar a entidade a partir do banco.
	private EmailMessage()
	{
	}

	public EmailMessage(
		string subject,
		string bodyHtml,
		string recipient
	)
	{
		Subject = subject;
		BodyHtml = bodyHtml;
		Recipient = recipient;
		Sent = false;
		SentAt = null;
		AttemptCount = 0;
		LastAttemptAt = null;
		LastError = null;
	}

	public void MarkAsSent()
	{
		Sent = true;
		SentAt = DateTime.UtcNow;
		AttemptCount++;
		LastAttemptAt = SentAt;
		LastError = null;
	}

	public void RegisterFailedAttempt(string error)
	{
		AttemptCount++;
		LastAttemptAt = DateTime.UtcNow;
		LastError = error.Length > MaxLastErrorLength
			? error[..MaxLastErrorLength]
			: error;
	}

	// Uma mensagem que falhou vezes demais para de ser tentada, em vez de bater no servidor SMTP
	// a cada rodada para sempre. Ela continua na tabela, com o último erro, para inspeção.
	public bool HasExhaustedAttempts(int maxAttempts)
	{
		return AttemptCount >= maxAttempts;
	}
}
