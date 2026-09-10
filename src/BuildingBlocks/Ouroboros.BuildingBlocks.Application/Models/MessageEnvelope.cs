namespace Ouroboros.BuildingBlocks.Application;

// O que atravessa a fronteira entre dois serviços. Carrega os metadados de transporte; o conteúdo de
// negócio vai serializado em Payload, num contrato próprio (ex.: EmailNotificationRequestedV1).
//
// MessageId é estável entre republicações: é ele que permite ao consumidor reconhecer uma reentrega
// e não fazer o trabalho duas vezes.
public sealed record MessageEnvelope(
	Guid MessageId,
	string Producer,
	string MessageType,
	int SchemaVersion,
	DateTime OccurredAt,
	string? CorrelationId,
	string Payload
);
