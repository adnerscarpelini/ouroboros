using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.BuildingBlocks.Infrastructure;

// A outra metade do padrão Outbox: o caso de uso grava a mensagem na mesma transação do dado de
// negócio (IEmailQueueService) e este processo entrega depois, fora dela. É isso que permite dizer
// "ou o usuário foi criado com e-mail de confirmação a caminho, ou nada aconteceu", sem depender de
// o servidor SMTP estar de pé no instante do cadastro. Ver docs/0007.
//
// Só cuida do agendamento; o trabalho em si é do EmailOutboxDispatcher.
public sealed class EmailOutboxProcessor<TDbContext> : BackgroundService
	where TDbContext : AppDbContext
{
	private readonly IServiceScopeFactory _serviceScopeFactory;
	private readonly EmailOutboxOptions _options;
	private readonly ILogger<EmailOutboxProcessor<TDbContext>> _logger;

	public EmailOutboxProcessor(
		IServiceScopeFactory serviceScopeFactory,
		EmailOutboxOptions options,
		ILogger<EmailOutboxProcessor<TDbContext>> logger
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

			var dispatcher = scope.ServiceProvider.GetRequiredService<EmailOutboxDispatcher<TDbContext>>();

			await dispatcher.DispatchPendingAsync(cancellationToken);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			// Catch justificado: uma exceção que escapasse daqui encerraria o BackgroundService em
			// silêncio, e a fila pararia de ser processada até o próximo restart da aplicação.
			_logger.LogError(exception, "Falha ao processar a fila de e-mails. Nova tentativa na próxima rodada.");
		}
	}
}
