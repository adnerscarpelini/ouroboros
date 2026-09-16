using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Ouroboros.Services.Notifications.Application;

namespace Ouroboros.Services.Notifications.Infrastructure;

public sealed class SmtpEmailSenderService : IEmailSender
{
	private readonly SmtpOptions _options;

	public SmtpEmailSenderService(SmtpOptions options)
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

		await smtpClient.ConnectAsync(
			host: _options.Host,
			port: _options.Port,
			options: ParseSecureSocketOptions(_options.SecureSocketOptions),
			cancellationToken: cancellationToken
		);

		if (!string.IsNullOrWhiteSpace(_options.Username))
		{
			await smtpClient.AuthenticateAsync(
				userName: _options.Username,
				password: _options.Password ?? string.Empty,
				cancellationToken: cancellationToken
			);
		}

		await smtpClient.SendAsync(message, cancellationToken);

		await smtpClient.DisconnectAsync(quit: true, cancellationToken: cancellationToken);
	}

	// Configuração inválida é falha de subida, não silêncio: cair para "Auto" quando alguém digitou
	// errado a política de TLS transformaria um erro de configuração em conexão sem criptografia.
	private static SecureSocketOptions ParseSecureSocketOptions(string configuredValue)
	{
		return Enum.TryParse<SecureSocketOptions>(configuredValue, ignoreCase: true, out var parsed)
			? parsed
			: throw new InvalidOperationException($"Configuração 'Smtp:SecureSocketOptions' inválida: '{configuredValue}'.");
	}
}
