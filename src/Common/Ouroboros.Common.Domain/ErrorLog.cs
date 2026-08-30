namespace Ouroboros.Common.Domain;

public sealed class ErrorLog
{
	public Guid Id { get; private set; }
	public DateTime OccurredAt { get; private set; }
	public string Source { get; private set; } = null!;
	public string ExceptionType { get; private set; } = null!;
	public string Message { get; private set; } = null!;
	public string? StackTrace { get; private set; }
	public string? RequestPath { get; private set; }
	public string? TraceId { get; private set; }

	// Construtor sem parâmetros exclusivo para o EF Core materializar a entidade a partir do banco.
	// Com ele presente, o EF usa "set" privado em cada propriedade em vez do construtor público abaixo.
	private ErrorLog()
	{
	}

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
