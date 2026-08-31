namespace Ouroboros.Common.Application;

public interface IEmailQueueService
{
	Task<long> EnqueueAsync(
		string subject,
		string bodyHtml,
		string recipient,
		CancellationToken cancellationToken
	);
}
