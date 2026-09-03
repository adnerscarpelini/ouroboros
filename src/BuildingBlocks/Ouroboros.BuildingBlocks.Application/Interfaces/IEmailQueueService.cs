namespace Ouroboros.BuildingBlocks.Application;

public interface IEmailQueueService
{
	Task<long> EnqueueAsync(
		string subject,
		string bodyHtml,
		string recipient,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);
}
