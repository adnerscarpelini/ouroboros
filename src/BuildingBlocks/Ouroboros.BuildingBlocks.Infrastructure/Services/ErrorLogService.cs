using Ouroboros.BuildingBlocks.Application;
using Ouroboros.BuildingBlocks.Domain;

namespace Ouroboros.BuildingBlocks.Infrastructure;

public sealed class ErrorLogService : IErrorLogService
{
	private readonly DbSession _session;

	public ErrorLogService(DbSession session)
	{
		_session = session;
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

		await _session.OpenAsync(cancellationToken);
		await using var command = new Npgsql.NpgsqlCommand(
			"""
			INSERT INTO common.error_logs (
				external_id,
				created_at,
				updated_at,
				source,
				exception_type,
				message,
				stack_trace,
				request_path,
				trace_id
			)
			VALUES (
				@external_id,
				@created_at,
				NULL,
				@source,
				@exception_type,
				@message,
				@stack_trace,
				@request_path,
				@trace_id
			);
			""",
			(Npgsql.NpgsqlConnection)_session.Connection);
		command.Parameters.AddWithValue("external_id", errorLog.ExternalId);
		command.Parameters.AddWithValue("created_at", errorLog.CreatedAt);
		command.Parameters.AddWithValue("source", errorLog.Source);
		command.Parameters.AddWithValue("exception_type", errorLog.ExceptionType);
		command.Parameters.AddWithValue("message", errorLog.Message);
		command.Parameters.AddWithValue("stack_trace", (object?)errorLog.StackTrace ?? DBNull.Value);
		command.Parameters.AddWithValue("request_path", (object?)errorLog.RequestPath ?? DBNull.Value);
		command.Parameters.AddWithValue("trace_id", (object?)errorLog.TraceId ?? DBNull.Value);
		await command.ExecuteNonQueryAsync(cancellationToken);
	}
}
