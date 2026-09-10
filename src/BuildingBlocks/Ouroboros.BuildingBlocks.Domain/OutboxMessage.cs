namespace Ouroboros.BuildingBlocks.Domain;

// A linha da outbox transacional: a intenção de publicar uma mensagem, gravada na MESMA transação do
// dado de negócio que a originou. Ou os dois são gravados, ou nenhum — é isso que impede um cadastro
// sem e-mail de confirmação a caminho, e o contrário também.
//
// Ela nunca sai do banco do produtor. O que atravessa a fronteira do serviço é a mensagem publicada
// depois do commit, não esta linha. Ver docs/0007 - Fila de E-mails (Outbox).md.
public sealed class OutboxMessage : Entity
{
	// O erro é guardado só para diagnóstico; truncar evita que a resposta inteira de um broker
	// fora do ar inche a tabela.
	private const int MaxLastErrorLength = 500;

	// Identificação lógica da mensagem no transporte (ex.: "notifications.email.requested"), validada
	// pelo consumidor antes de desserializar. Nome de classe CLR não é contrato de rede.
	public string MessageType { get; private set; } = null!;
	public int SchemaVersion { get; private set; }
	// Quem solicitou. Compõe com o ExternalId a chave de deduplicação do consumidor.
	public string Producer { get; private set; } = null!;
	public string Payload { get; private set; } = null!;
	// O X-Correlation-Id da requisição de origem. É o que liga o log do produtor ao do consumidor
	// numa mensagem publicada minutos depois — não substitui o ExternalId como chave de idempotência.
	public string? CorrelationId { get; private set; }
	public OutboxMessageStatus Status { get; private set; }
	public DateTime? PublishedAt { get; private set; }
	public int AttemptCount { get; private set; }
	public DateTime? LastAttemptAt { get; private set; }
	// Quando a próxima tentativa de publicação pode acontecer. Null significa "agora".
	public DateTime? NextAttemptAt { get; private set; }
	public string? LastError { get; private set; }

	// Construtor sem parâmetros exclusivo para o EF Core materializar a entidade a partir do banco.
	private OutboxMessage()
	{
	}

	public OutboxMessage(
		string producer,
		string messageType,
		int schemaVersion,
		string payload,
		string? correlationId
	)
	{
		Producer = producer;
		MessageType = messageType;
		SchemaVersion = schemaVersion;
		Payload = payload;
		CorrelationId = correlationId;
		Status = OutboxMessageStatus.Pending;
		AttemptCount = 0;
	}

	public void MarkAsPublished()
	{
		Status = OutboxMessageStatus.Published;
		PublishedAt = DateTime.UtcNow;
		AttemptCount++;
		LastAttemptAt = PublishedAt;
		NextAttemptAt = null;
		LastError = null;
	}

	// Sem descarte por indisponibilidade: uma mensagem que não conseguiu ser publicada continua
	// pendente e volta na rodada seguinte, só que mais tarde a cada tentativa. Broker fora do ar
	// atrasa a notificação; não a perde.
	public void RegisterFailedAttempt(
		string error,
		DateTime nextAttemptAt
	)
	{
		AttemptCount++;
		LastAttemptAt = DateTime.UtcNow;
		NextAttemptAt = nextAttemptAt;
		LastError = error.Length > MaxLastErrorLength
			? error[..MaxLastErrorLength]
			: error;
	}

	public bool IsDueAt(DateTime instant)
	{
		return Status == OutboxMessageStatus.Pending
			&& (NextAttemptAt is null || NextAttemptAt <= instant);
	}
}
