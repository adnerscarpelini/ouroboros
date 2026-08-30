namespace Ouroboros.BuildingBlocks.Domain;

public sealed class ErrorLog
{
	public Guid Id { get; }
	public DateTime OccurredAt { get; }
	public string Source { get; }
	public string ExceptionType { get; }
	public string Message { get; }
	public string? StackTrace { get; }
	public string? RequestPath { get; }
	public string? TraceId { get; }

	public ErrorLog(
		string source,
		string exceptionType,
		string message,
		string? stackTrace,
		string? requestPath,
		string? traceId
	)
	{
		Id = Guid.NewGuid();
		OccurredAt = DateTime.UtcNow;
		Source = source;
		ExceptionType = exceptionType;
		Message = message;
		StackTrace = stackTrace;
		RequestPath = requestPath;
		TraceId = traceId;
	}
}
