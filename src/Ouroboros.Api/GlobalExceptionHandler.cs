using Microsoft.AspNetCore.Diagnostics;
using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.Api;

// IExceptionHandler é registrado como Singleton pelo framework, mas IErrorLogService é Scoped
// (depende do DbContext). Por isso resolvemos via IServiceScopeFactory, criando um escopo novo
// a cada erro, em vez de injetar IErrorLogService direto no construtor.
public sealed class GlobalExceptionHandler : IExceptionHandler
{
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
		_logger.LogError(exception, "Erro não tratado capturado pelo GlobalExceptionHandler");

		await using var scope = _serviceScopeFactory.CreateAsyncScope();
		var errorLogService = scope.ServiceProvider.GetRequiredService<IErrorLogService>();

		await errorLogService.AddAsync(
			exception: exception,
			source: "Api",
			requestPath: httpContext.Request.Path,
			traceId: httpContext.TraceIdentifier,
			cancellationToken: cancellationToken
		);

		httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

		await httpContext.Response.WriteAsJsonAsync(
			new { message = "Ocorreu um erro inesperado." },
			cancellationToken
		);

		return true;
	}
}
