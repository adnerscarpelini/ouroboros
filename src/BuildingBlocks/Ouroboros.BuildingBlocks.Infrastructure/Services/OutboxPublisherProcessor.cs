using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.BuildingBlocks.Infrastructure;

// A segunda metade do padrão Outbox: o caso de uso grava a solicitação na mesma transação do dado de
// negócio (IOutboxMessageQueue) e este processo publica depois, fora dela. É isso que permite dizer
// "ou o usuário foi criado com a notificação a caminho, ou nada aconteceu", sem depender de o
// transporte estar de pé no instante do cadastro. Ver docs/0007.
//
// Só cuida do agendamento; o trabalho em si é do OutboxPublisher.
public sealed class OutboxPublisherProcessor<TDbContext> : BackgroundService
	where TDbContext : AppDbContext
{
	private readonly IServiceScopeFactory _serviceScopeFactory;
	private readonly OutboxOptions _options;
	private readonly OutboxProducer _producer;
	private readonly ILogger<OutboxPublisherProcessor<TDbContext>> _logger;

	public OutboxPublisherProcessor(
		IServiceScopeFactory serviceScopeFactory,
		OutboxOptions options,
		OutboxProducer producer,
		ILogger<OutboxPublisherProcessor<TDbContext>> logger
	)
	{
		_serviceScopeFactory = serviceScopeFactory;
		_options = options;
		_producer = producer;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!HasMessageTransport())
		{
			// Enquanto não houver broker configurado, a outbox continua sendo gravada normalmente e
			// nada é publicado. Avisar uma vez na subida é melhor do que falhar de 15 em 15 segundos
			// contra um transporte que ninguém registrou.
			_logger.LogWarning(
				"Nenhum IMessagePublisher registrado: as mensagens do produtor '{Producer}' ficam pendentes na outbox até a mensageria ser configurada.",
				_producer.Name
			);

			return;
		}

		using var timer = new PeriodicTimer(_options.PollingInterval);

		try
		{
			do
			{
				await PublishSafelyAsync(stoppingToken);
			}
			while (await timer.WaitForNextTickAsync(stoppingToken));
		}
		catch (OperationCanceledException)
		{
			// Encerramento normal da aplicação.
		}
	}

	private bool HasMessageTransport()
	{
		using var scope = _serviceScopeFactory.CreateScope();

		return scope.ServiceProvider.GetService<IMessagePublisher>() is not null;
	}

	private async Task PublishSafelyAsync(CancellationToken cancellationToken)
	{
		try
		{
			// Escopo próprio a cada rodada: o DbContext é Scoped e este serviço é Singleton.
			await using var scope = _serviceScopeFactory.CreateAsyncScope();

			var publisher = scope.ServiceProvider.GetRequiredService<OutboxPublisher<TDbContext>>();

			await publisher.PublishPendingAsync(cancellationToken);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			// Catch justificado: uma exceção que escapasse daqui encerraria o BackgroundService em
			// silêncio, e a outbox pararia de ser publicada até o próximo restart da aplicação.
			_logger.LogError(exception, "Falha ao publicar a outbox. Nova tentativa na próxima rodada.");
		}
	}
}
