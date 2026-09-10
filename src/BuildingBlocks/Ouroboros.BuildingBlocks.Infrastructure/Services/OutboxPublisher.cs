using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ouroboros.BuildingBlocks.Application;
using Ouroboros.BuildingBlocks.Domain;

namespace Ouroboros.BuildingBlocks.Infrastructure;

// Uma passada pela outbox: pega o próximo lote de mensagens vencidas, tenta publicar cada uma e grava
// o resultado. Separado do OutboxPublisherProcessor de propósito — aqui mora o "o que fazer", lá o
// "de quanto em quanto tempo". É essa separação que permite testar a publicação sem temporizador.
//
// TDbContext é o DbContext do serviço: cada produtor publica só a própria outbox, na própria base.
public sealed class OutboxPublisher<TDbContext>
	where TDbContext : AppDbContext
{
	private const int MaxBackoffExponent = 16;

	private readonly TDbContext _dbContext;
	private readonly IMessagePublisher _messagePublisher;
	private readonly OutboxOptions _options;
	private readonly ILogger<OutboxPublisher<TDbContext>> _logger;

	public OutboxPublisher(
		TDbContext dbContext,
		IMessagePublisher messagePublisher,
		OutboxOptions options,
		ILogger<OutboxPublisher<TDbContext>> logger
	)
	{
		_dbContext = dbContext;
		_messagePublisher = messagePublisher;
		_options = options;
		_logger = logger;
	}

	// Devolve quantas mensagens foram tentadas nesta passada.
	public async Task<int> PublishPendingAsync(CancellationToken cancellationToken)
	{
		var now = DateTime.UtcNow;

		var pendingMessages = await _dbContext.Set<OutboxMessage>()
			.Where(m => m.Status == OutboxMessageStatus.Pending
				&& (m.NextAttemptAt == null || m.NextAttemptAt <= now))
			.OrderBy(m => m.Id)
			.Take(_options.BatchSize)
			.ToListAsync(cancellationToken);

		if (pendingMessages.Count == 0)
		{
			return 0;
		}

		foreach (var pendingMessage in pendingMessages)
		{
			await PublishOneAsync(
				pendingMessage: pendingMessage,
				cancellationToken: cancellationToken
			);
		}

		await _dbContext.SaveChangesAsync(cancellationToken);

		return pendingMessages.Count;
	}

	private async Task PublishOneAsync(
		OutboxMessage pendingMessage,
		CancellationToken cancellationToken
	)
	{
		try
		{
			await _messagePublisher.PublishAsync(
				envelope: new MessageEnvelope(
					MessageId: pendingMessage.ExternalId,
					Producer: pendingMessage.Producer,
					MessageType: pendingMessage.MessageType,
					SchemaVersion: pendingMessage.SchemaVersion,
					OccurredAt: pendingMessage.CreatedAt,
					CorrelationId: pendingMessage.CorrelationId,
					Payload: pendingMessage.Payload
				),
				cancellationToken: cancellationToken
			);

			pendingMessage.MarkAsPublished();
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			// Catch justificado: a falha de uma mensagem não pode abortar o lote inteiro, e a
			// indisponibilidade do transporte não pode descartar a solicitação. O erro fica na
			// própria linha e a mensagem volta mais tarde, com espera crescente.
			pendingMessage.RegisterFailedAttempt(
				error: exception.Message,
				nextAttemptAt: CalculateNextAttempt(pendingMessage.AttemptCount)
			);

			_logger.LogWarning(
				exception,
				"Falha ao publicar a mensagem {MessageId} ({MessageType}). Tentativa {AttemptCount}.",
				pendingMessage.ExternalId,
				pendingMessage.MessageType,
				pendingMessage.AttemptCount
			);
		}
	}

	// Espera exponencial com jitter: sem o jitter, várias instâncias que falharam na mesma rodada
	// voltariam a bater no transporte exatamente no mesmo instante.
	private DateTime CalculateNextAttempt(int previousAttemptCount)
	{
		var exponent = Math.Min(previousAttemptCount, MaxBackoffExponent);

		var delayTicks = _options.InitialRetryDelay.Ticks * (1L << exponent);

		var delay = delayTicks > _options.MaxRetryDelay.Ticks || delayTicks < 0
			? _options.MaxRetryDelay
			: TimeSpan.FromTicks(delayTicks);

		var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));

		return DateTime.UtcNow.Add(delay).Add(jitter);
	}
}
