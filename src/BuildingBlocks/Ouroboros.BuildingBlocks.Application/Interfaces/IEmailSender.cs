namespace Ouroboros.BuildingBlocks.Application;

public interface IEmailSender
{
	// Entrega de fato a mensagem. Quem enfileira é o IEmailQueueService, dentro da transação do
	// caso de uso; quem entrega é este, depois, fora dela — ver docs/0007.
	Task SendAsync(
		string recipient,
		string subject,
		string bodyHtml,
		// Cancela a operação em andamento se a aplicação estiver sendo encerrada.
		CancellationToken cancellationToken
	);
}
