using System.Text.Json;
using Npgsql;
using Ouroboros.BuildingBlocks.Application;
using Ouroboros.BuildingBlocks.Domain;

namespace Ouroboros.BuildingBlocks.Infrastructure;

public sealed class OutboxMessageQueueService : IOutboxMessageQueue
{
	private readonly DbSession _session;
	private readonly ICorrelationIdAccessor _correlationIdAccessor;
	private readonly OutboxProducer _producer;

	public OutboxMessageQueueService(
		DbSession session,
		ICorrelationIdAccessor correlationIdAccessor,
		OutboxProducer producer)
	{
		_session = session;
		_correlationIdAccessor = correlationIdAccessor;
		_producer = producer;
	}

	public Guid Add<TPayload>(string messageType, int schemaVersion, TPayload payload)
	{
		var outboxMessage = new OutboxMessage(
			producer: _producer.Name,
			messageType: messageType,
			schemaVersion: schemaVersion,
			payload: JsonSerializer.Serialize(payload, MessageSerialization.Options),
			correlationId: _correlationIdAccessor.GetCorrelationId());

		_session.Enqueue(async (connection, transaction, cancellationToken) =>
		{
			await using var command = new NpgsqlCommand(
				"""
				INSERT INTO common.outbox_messages (
					external_id,
					created_at,
					updated_at,
					message_type,
					schema_version,
					producer,
					payload,
					correlation_id,
					status,
					attempt_count
				)
				VALUES (
					@external_id,
					@created_at,
					NULL,
					@message_type,
					@schema_version,
					@producer,
					@payload,
					@correlation_id,
					@status,
					0
				);
				""",
				(NpgsqlConnection)connection,
				(NpgsqlTransaction)transaction);
			command.Parameters.AddWithValue("external_id", outboxMessage.ExternalId);
			command.Parameters.AddWithValue("created_at", outboxMessage.CreatedAt);
			command.Parameters.AddWithValue("message_type", outboxMessage.MessageType);
			command.Parameters.AddWithValue("schema_version", outboxMessage.SchemaVersion);
			command.Parameters.AddWithValue("producer", outboxMessage.Producer);
			command.Parameters.AddWithValue("payload", outboxMessage.Payload);
			command.Parameters.AddWithValue("correlation_id", (object?)outboxMessage.CorrelationId ?? DBNull.Value);
			command.Parameters.AddWithValue("status", (int)outboxMessage.Status);
			await command.ExecuteNonQueryAsync(cancellationToken);
		});
		return outboxMessage.ExternalId;
	}
}
