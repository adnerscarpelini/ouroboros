using Ouroboros.BuildingBlocks.Application;
using Ouroboros.BuildingBlocks.Domain;

namespace Ouroboros.BuildingBlocks.Infrastructure;

public sealed class ErrorLogService : IErrorLogService
{
	private readonly AppDbContext _dbContext;

	public ErrorLogService(AppDbContext dbContext)
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

		_dbContext.Set<ErrorLog>().Add(errorLog);

		await _dbContext.SaveChangesAsync(cancellationToken);
	}
}
