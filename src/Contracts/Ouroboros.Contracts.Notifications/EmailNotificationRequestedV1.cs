namespace Ouroboros.Contracts.Notifications;

// Solicitação explícita de envio de e-mail. O produtor diz para quem, com qual template e com quais
// dados; ele não monta HTML, não conhece SMTP e não decide quando entregar.
//
// Os metadados de transporte (MessageId, Producer, OccurredAt, MessageType, SchemaVersion,
// CorrelationId) viajam no envelope, não aqui — ver Ouroboros.BuildingBlocks.Application.MessageEnvelope.
public sealed record EmailNotificationRequestedV1(
	string Recipient,
	string TemplateKey,
	int TemplateVersion,
	string Locale,
	// Valores dos placeholders do template. Nunca senha, JWT ou entidade de domínio serializada.
	IReadOnlyDictionary<string, string> Data,
	// Mesmo instante de expiração do token que originou a solicitação: entregar depois disso
	// só produziria um link morto na caixa de entrada do usuário.
	DateTime ExpiresAt
);
