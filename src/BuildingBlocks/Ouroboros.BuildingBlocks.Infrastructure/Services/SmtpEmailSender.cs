using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.BuildingBlocks.Infrastructure;

public sealed class SmtpEmailSender : IEmailSender
{
	private readonly EmailOutboxOptions _options;

	public SmtpEmailSender(EmailOutboxOptions options)
	{
		_options = options;
	}

	public async Task SendAsync(
		string recipient,
		string subject,
		string bodyHtml,
		CancellationToken cancellationToken
	)
	{
		var message = new MimeMessage();
		message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
		message.To.Add(MailboxAddress.Parse(recipient));
		message.Subject = subject;
		message.Body = new BodyBuilder { HtmlBody = bodyHtml }.ToMessageBody();

		using var smtpClient = new SmtpClient();

		// Auto: usa TLS se o servidor anunciar, texto puro se não. O servidor de desenvolvimento
		// (Mailpit) não tem TLS; um servidor real tem, e a mesma configuração serve para os dois.
		await smtpClient.ConnectAsync(
			host: _options.SmtpHost,
			port: _options.SmtpPort,
			options: SecureSocketOptions.Auto,
			cancellationToken: cancellationToken
		);

		await smtpClient.SendAsync(message, cancellationToken);

		await smtpClient.DisconnectAsync(quit: true, cancellationToken: cancellationToken);
	}
}
