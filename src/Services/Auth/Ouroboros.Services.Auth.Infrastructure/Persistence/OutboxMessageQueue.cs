using System.Text.Json;
using Npgsql;
using Ouroboros.BuildingBlocks.Application;
using Ouroboros.BuildingBlocks.Domain;
using Ouroboros.BuildingBlocks.Infrastructure;

namespace Ouroboros.Services.Auth.Infrastructure;

public sealed class OutboxMessageQueue : IOutboxMessageQueue
{
	private readonly DbSession _session;
	private readonly ICorrelationIdAccessor _correlationIdAccessor;
	private readonly OutboxProducer _producer;

	public OutboxMessageQueue(DbSession session, ICorrelationIdAccessor correlationIdAccessor, OutboxProducer producer)
	{
		_session = session;
		_correlationIdAccessor = correlationIdAccessor;
		_producer = producer;
	}

	public Guid Add<TPayload>(string messageType, int schemaVersion, TPayload payload)
	{
		var message = new OutboxMessage(_producer.Name, messageType, schemaVersion,
			JsonSerializer.Serialize(payload, MessageSerialization.Options), _correlationIdAccessor.GetCorrelationId());
		_session.Enqueue(async (connection, transaction, cancellationToken) =>
		{
			await using var command = new NpgsqlCommand(
				"""
				INSERT INTO common.outbox_messages (
					external_id,
					created_at,
					updated_at,
					producer,
					message_type,
					schema_version,
					payload,
					correlation_id,
					status,
					published_at,
					attempt_count,
					last_attempt_at,
					next_attempt_at,
					last_error
				)
				VALUES (
					@external_id,
					@created_at,
					@updated_at,
					@producer,
					@message_type,
					@schema_version,
					@payload,
					@correlation_id,
					@status,
					@published_at,
					@attempt_count,
					@last_attempt_at,
					@next_attempt_at,
					@last_error
				);
				""",
				(NpgsqlConnection)connection,
				(NpgsqlTransaction)transaction);
			command.Parameters.AddWithValue("external_id", message.ExternalId);
			command.Parameters.AddWithValue("created_at", message.CreatedAt);
			command.Parameters.AddWithValue("updated_at", (object?)message.UpdatedAt ?? DBNull.Value);
			command.Parameters.AddWithValue("producer", message.Producer);
			command.Parameters.AddWithValue("message_type", message.MessageType);
			command.Parameters.AddWithValue("schema_version", message.SchemaVersion);
			command.Parameters.AddWithValue("payload", message.Payload);
			command.Parameters.AddWithValue("correlation_id", (object?)message.CorrelationId ?? DBNull.Value);
			command.Parameters.AddWithValue("status", (int)message.Status);
			command.Parameters.AddWithValue("published_at", DBNull.Value);
			command.Parameters.AddWithValue("attempt_count", message.AttemptCount);
			command.Parameters.AddWithValue("last_attempt_at", DBNull.Value);
			command.Parameters.AddWithValue("next_attempt_at", DBNull.Value);
			command.Parameters.AddWithValue("last_error", DBNull.Value);
			await command.ExecuteNonQueryAsync(cancellationToken);
		});
		return message.ExternalId;
	}
}
