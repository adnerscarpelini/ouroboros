namespace Ouroboros.Contracts.Notifications;

// Identificador lógico de quem solicitou a notificação. Compõe, com o MessageId, a chave de
// deduplicação do consumidor — dois produtores diferentes podem gerar o mesmo UUID sem colidir.
public static class NotificationProducers
{
	public const string Auth = "auth";
}
