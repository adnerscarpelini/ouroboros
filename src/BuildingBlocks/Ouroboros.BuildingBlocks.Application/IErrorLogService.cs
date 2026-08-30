namespace Ouroboros.BuildingBlocks.Application;

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
