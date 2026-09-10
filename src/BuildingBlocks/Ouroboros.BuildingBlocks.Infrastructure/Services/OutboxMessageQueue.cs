using System.Text.Json;
using Ouroboros.BuildingBlocks.Application;
using Ouroboros.BuildingBlocks.Domain;

namespace Ouroboros.BuildingBlocks.Infrastructure;

public sealed class OutboxMessageQueue : IOutboxMessageQueue
{
	private readonly AppDbContext _dbContext;
	private readonly ICorrelationIdAccessor _correlationIdAccessor;
	private readonly OutboxProducer _producer;

	public OutboxMessageQueue(
		AppDbContext dbContext,
		ICorrelationIdAccessor correlationIdAccessor,
		OutboxProducer producer
	)
	{
		_dbContext = dbContext;
		_correlationIdAccessor = correlationIdAccessor;
		_producer = producer;
	}

	public Guid Add<TPayload>(
		string messageType,
		int schemaVersion,
		TPayload payload
	)
	{
		var outboxMessage = new OutboxMessage(
			producer: _producer.Name,
			messageType: messageType,
			schemaVersion: schemaVersion,
			payload: JsonSerializer.Serialize(payload, MessageSerialization.Options),
			correlationId: _correlationIdAccessor.GetCorrelationId()
		);

		// Sem SaveChanges de propósito: a linha entra na mesma transação do dado de negócio e é
		// gravada pelo commit do caso de uso. O ExternalId já existe aqui porque Entity o gera no
		// construtor — não é preciso ir ao banco para descobri-lo.
		_dbContext.Set<OutboxMessage>().Add(outboxMessage);

		return outboxMessage.ExternalId;
	}
}
