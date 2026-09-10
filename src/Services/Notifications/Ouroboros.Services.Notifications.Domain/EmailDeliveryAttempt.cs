using Ouroboros.BuildingBlocks.Domain;

namespace Ouroboros.Services.Notifications.Domain;

// O histórico por tentativa: quando começou, quando terminou e com que resultado. É o que permite
// responder "por que este e-mail não chegou" sem depender de log de processo, que é volátil.
//
// Uma tentativa que começou e nunca foi concluída (Succeeded nulo) é o caso ambíguo do SMTP: o
// servidor pode ter aceitado a mensagem antes de o processo cair.
public sealed class EmailDeliveryAttempt : Entity
{
	public long EmailDeliveryId { get; private set; }
	public int AttemptNumber { get; private set; }
	public DateTime StartedAt { get; private set; }
	public DateTime? CompletedAt { get; private set; }
	public bool? Succeeded { get; private set; }
	public string? Error { get; private set; }

	public EmailDelivery Delivery { get; private set; } = null!;

	// Construtor sem parâmetros exclusivo para o EF Core materializar a entidade a partir do banco.
	private EmailDeliveryAttempt()
	{
	}

	public EmailDeliveryAttempt(
		EmailDelivery delivery,
		int attemptNumber
	)
	{
		Delivery = delivery;
		AttemptNumber = attemptNumber;
		StartedAt = DateTime.UtcNow;
	}

	public void CompleteAsSucceeded()
	{
		CompletedAt = DateTime.UtcNow;
		Succeeded = true;
		Error = null;
	}

	public void CompleteAsFailed(string error)
	{
		CompletedAt = DateTime.UtcNow;
		Succeeded = false;
		Error = error;
	}
}
