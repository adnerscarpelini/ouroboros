namespace Ouroboros.BuildingBlocks.Application;

public interface IOutboxMessageQueue
{
	// Só adiciona a mensagem ao contexto da unidade de trabalho em andamento: não grava, não confirma
	// transação. Quem decide o commit continua sendo o caso de uso, e é isso que garante que a
	// solicitação e o dado de negócio sejam gravados juntos ou não sejam gravados.
	//
	// Devolve o MessageId já gerado, para o caso de uso correlacionar (ex.: guardá-lo no token) antes
	// mesmo do commit — sem precisar de um SaveChanges só para descobrir um id.
	Guid Add<TPayload>(
		string messageType,
		int schemaVersion,
		TPayload payload
	);
}
