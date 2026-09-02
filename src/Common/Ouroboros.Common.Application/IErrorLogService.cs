namespace Ouroboros.Common.Application;

public interface IErrorLogService
{
	Task AddAsync(
		Exception exception,
		string source,
		string? requestPath,
		string? traceId,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);
}
