using Ouroboros.Common.Application;
using Ouroboros.Common.Domain;

namespace Ouroboros.Common.Infrastructure;

public sealed class ErrorLogService : IErrorLogService
{
	private readonly CommonDbContext _dbContext;

	public ErrorLogService(CommonDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public async Task AddAsync(
		Exception exception,
		string source,
		string? requestPath,
		string? traceId,
		CancellationToken cancellationToken
	)
	{
		var errorLog = new ErrorLog(
			source: source,
			exceptionType: exception.GetType().FullName ?? exception.GetType().Name,
			message: exception.Message,
			stackTrace: exception.StackTrace,
			requestPath: requestPath,
			traceId: traceId
		);

		_dbContext.ErrorLogs.Add(errorLog);

		await _dbContext.SaveChangesAsync(cancellationToken);
	}
}
