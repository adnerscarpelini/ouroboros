using Ouroboros.Services.Notifications.Domain;

namespace Ouroboros.Services.Notifications.Application;

public interface IEmailDeliveryRepository
{
	// Só marca a entrega para inclusão — a gravação acontece no IUnitOfWork do caso de uso.
	void Add(EmailDelivery delivery);

	Task<EmailDelivery?> GetByRequestAsync(
		string producer,
		Guid requestId,
		// Cancela a operação em andamento se a aplicação estiver sendo encerrada.
		CancellationToken cancellationToken
	);

	// As entregas cuja hora chegou, em lote. Traz o histórico de tentativas junto porque o dispatcher
	// registra uma nova a cada passada.
	Task<IReadOnlyCollection<EmailDelivery>> GetDueForDeliveryAsync(
		DateTime instant,
		int batchSize,
		// Cancela a operação em andamento se a aplicação estiver sendo encerrada.
		CancellationToken cancellationToken
	);
}
