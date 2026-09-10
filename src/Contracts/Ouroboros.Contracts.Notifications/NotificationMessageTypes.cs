namespace Ouroboros.Contracts.Notifications;

// Identificação lógica da mensagem no transporte. É isso que o consumidor valida antes de
// desserializar — nunca o nome da classe CLR, que pode ser renomeado sem quebrar o contrato.
public static class NotificationMessageTypes
{
	public const string EmailRequested = "notifications.email.requested";
	public const int EmailRequestedSchemaVersion = 1;
}
