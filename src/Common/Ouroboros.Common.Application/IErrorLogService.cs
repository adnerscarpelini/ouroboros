namespace Ouroboros.Common.Application;

public interface IErrorLogService
{
	Task AddAsync(
		Exception exception,
		string source,
		string? requestPath,
		string? traceId,
		CancellationToken cancellationToken
	);
}
