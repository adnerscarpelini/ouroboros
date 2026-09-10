using Ouroboros.BuildingBlocks.Application;
using Ouroboros.Contracts.Notifications;

namespace Ouroboros.Services.Notifications.Application;

// A porta de entrada do consumidor. Quem lê do transporte chama isto e só confirma o consumo depois
// que o commit local terminou — se o processo cair entre o commit e a confirmação, a mensagem volta
// e a deduplicação impede o segundo envio.
public interface IEmailDeliveryIntakeService
{
	Task<EmailIntakeResult> AcceptAsync(
		MessageEnvelope envelope,
		EmailNotificationRequestedV1 request,
		// Cancela a operação em andamento se a aplicação estiver sendo encerrada.
		CancellationToken cancellationToken
	);
}
