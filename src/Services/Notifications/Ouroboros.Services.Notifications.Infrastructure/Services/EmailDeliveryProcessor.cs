using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ouroboros.Services.Notifications.Application;

namespace Ouroboros.Services.Notifications.Infrastructure;

// Depois que a solicitação foi aceita e persistida, a entrega não volta mais para o transporte: quem
// retoma é este processo, a partir do banco de Notificações. Falha de SMTP nunca devolve a mensagem
// para a fila do broker — são políticas de retentativa separadas.
//
// Só cuida do agendamento; o trabalho em si é do EmailDeliveryDispatcher.
public sealed class EmailDeliveryProcessor : BackgroundService
{
	private readonly IServiceScopeFactory _serviceScopeFactory;
	private readonly EmailDeliveryOptions _options;
	private readonly ILogger<EmailDeliveryProcessor> _logger;

	public EmailDeliveryProcessor(
		IServiceScopeFactory serviceScopeFactory,
		EmailDeliveryOptions options,
		ILogger<EmailDeliveryProcessor> logger
	)
	{
		_serviceScopeFactory = serviceScopeFactory;
		_options = options;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var timer = new PeriodicTimer(_options.PollingInterval);

		try
		{
			do
			{
				await DispatchSafelyAsync(stoppingToken);
			}
			while (await timer.WaitForNextTickAsync(stoppingToken));
		}
		catch (OperationCanceledException)
		{
			// Encerramento normal da aplicação.
		}
	}

	private async Task DispatchSafelyAsync(CancellationToken cancellationToken)
	{
		try
		{
			// Escopo próprio a cada rodada: o DbContext é Scoped e este serviço é Singleton.
			await using var scope = _serviceScopeFactory.CreateAsyncScope();

			var dispatcher = scope.ServiceProvider.GetRequiredService<EmailDeliveryDispatcher>();

			await dispatcher.DispatchDueAsync(cancellationToken);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			// Catch justificado: uma exceção que escapasse daqui encerraria o BackgroundService em
			// silêncio, e a fila pararia de ser processada até o próximo restart da aplicação. O
			// GlobalExceptionHandler só cobre o que chega por HTTP.
			_logger.LogError(exception, "Falha ao processar a fila de entregas. Nova tentativa na próxima rodada.");
		}
	}
}
