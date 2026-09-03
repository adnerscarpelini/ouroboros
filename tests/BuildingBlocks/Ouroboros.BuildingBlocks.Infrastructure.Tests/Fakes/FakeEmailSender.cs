using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.BuildingBlocks.Infrastructure.Tests;

public sealed class FakeEmailSender : IEmailSender
{
	private readonly List<string> _sentRecipients = new();

	// Quando definido, o envio para esse destinatário falha — usado para exercitar o caminho de erro.
	public string? FailingRecipient { get; set; }

	public IReadOnlyCollection<string> SentRecipients => _sentRecipients;

	public Task SendAsync(
		string recipient,
		string subject,
		string bodyHtml,
		CancellationToken cancellationToken
	)
	{
		if (recipient == FailingRecipient)
		{
			throw new InvalidOperationException("servidor SMTP indisponivel");
		}

		_sentRecipients.Add(recipient);

		return Task.CompletedTask;
	}
}
