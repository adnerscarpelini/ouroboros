using Microsoft.AspNetCore.Diagnostics;
using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.Services.Auth.Api;

// IExceptionHandler é registrado como Singleton pelo framework, mas IErrorLogService é Scoped
// (depende do DbContext). Por isso resolvemos via IServiceScopeFactory, criando um escopo novo
// a cada erro, em vez de injetar IErrorLogService direto no construtor.
public sealed class GlobalExceptionHandler : IExceptionHandler
{
	private const string CorrelationIdHeaderName = "X-Correlation-Id";

	private readonly IServiceScopeFactory _serviceScopeFactory;
	private readonly ILogger<GlobalExceptionHandler> _logger;

	public GlobalExceptionHandler(
		IServiceScopeFactory serviceScopeFactory,
		ILogger<GlobalExceptionHandler> logger
	)
	{
		_serviceScopeFactory = serviceScopeFactory;
		_logger = logger;
	}

	public async ValueTask<bool> TryHandleAsync(
		HttpContext httpContext,
		Exception exception,
		CancellationToken cancellationToken
	)
	{
		var traceId = ResolveTraceId(httpContext);

		_logger.LogError(exception, "Erro não tratado capturado pelo GlobalExceptionHandler. TraceId: {TraceId}", traceId);

		await using var scope = _serviceScopeFactory.CreateAsyncScope();
		var errorLogService = scope.ServiceProvider.GetRequiredService<IErrorLogService>();

		await errorLogService.AddAsync(
			exception: exception,
			source: "Api",
			requestPath: httpContext.Request.Path,
			traceId: traceId,
			cancellationToken: cancellationToken
		);

		httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

		await httpContext.Response.WriteAsJsonAsync(
			new { message = "Ocorreu um erro inesperado." },
			cancellationToken
		);

		return true;
	}

	// Prefere o X-Correlation-Id posto pelo Api Gateway: ele é o mesmo em todos os serviços que
	// atenderam a requisição. O TraceIdentifier é local a este processo e só serve como último
	// recurso, quando a Api é chamada direto (desenvolvimento), sem passar pelo gateway.
	private static string ResolveTraceId(HttpContext httpContext)
	{
		var correlationId = httpContext.Request.Headers[CorrelationIdHeaderName].FirstOrDefault();

		return string.IsNullOrWhiteSpace(correlationId)
			? httpContext.TraceIdentifier
			: correlationId;
	}
}
