namespace Ouroboros.Services.Notifications.Application;

public interface IEmailSender
{
	// Entrega de fato a mensagem ao provedor. Só o dispatcher chama: um caso de uso nunca espera por
	// um servidor SMTP, e um produtor nunca chega até aqui.
	Task SendAsync(
		string recipient,
		string subject,
		string bodyHtml,
		// Cancela a operação em andamento se a aplicação estiver sendo encerrada.
		CancellationToken cancellationToken
	);
}
