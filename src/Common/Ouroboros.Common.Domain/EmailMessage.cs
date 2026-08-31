namespace Ouroboros.Common.Domain;

public sealed class EmailMessage : Entity
{
	public string Subject { get; private set; } = null!;
	public string BodyHtml { get; private set; } = null!;
	public string Recipient { get; private set; } = null!;
	public bool Sent { get; private set; }
	public DateTime? SentAt { get; private set; }

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
	}

	public void MarkAsSent()
	{
		Sent = true;
		SentAt = DateTime.UtcNow;
	}
}
