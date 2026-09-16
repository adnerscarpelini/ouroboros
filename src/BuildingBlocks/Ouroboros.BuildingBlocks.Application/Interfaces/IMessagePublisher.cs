namespace Ouroboros.BuildingBlocks.Application;

// O transporte. Enquanto nenhuma implementação estiver registrada, a outbox continua sendo gravada
// normalmente e nada é publicado — ver OutboxPublisherProcessorService e a spec de mensageria.
public interface IMessagePublisher
{
	Task PublishAsync(
		MessageEnvelope envelope,
		// Cancela a operação em andamento se a aplicação estiver sendo encerrada.
		CancellationToken cancellationToken
	);
}
