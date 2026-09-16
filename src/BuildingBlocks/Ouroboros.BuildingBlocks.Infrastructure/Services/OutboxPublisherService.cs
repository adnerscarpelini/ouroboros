using Microsoft.Extensions.Logging;
using Npgsql;
using Ouroboros.BuildingBlocks.Application;
using Ouroboros.BuildingBlocks.Domain;

namespace Ouroboros.BuildingBlocks.Infrastructure;

public sealed class OutboxPublisherService
{
	private readonly DbSession _session;
	private readonly IMessagePublisher _publisher;
	private readonly OutboxOptions _options;
	private readonly ILogger<OutboxPublisherService> _logger;

	public OutboxPublisherService(
		DbSession session,
		IMessagePublisher publisher,
		OutboxOptions options,
		ILogger<OutboxPublisherService> logger
	)
	{
		_session = session;
		_publisher = publisher;
		_options = options;
		_logger = logger;
	}

	public async Task<int> PublishPendingAsync(CancellationToken cancellationToken)
	{
		await _session.OpenAsync(cancellationToken);

		var candidateIds = await SelectCandidateIdsAsync(cancellationToken);
		var publishedCount = 0;

		foreach (var id in candidateIds)
		{
			if (await TryPublishOneAsync(id, cancellationToken))
			{
				publishedCount++;
			}
		}

		return publishedCount;
	}

	private async Task<List<long>> SelectCandidateIdsAsync(CancellationToken cancellationToken)
	{
		var candidateIds = new List<long>();

		await using var command = new NpgsqlCommand(
			"""
			SELECT
				id
			FROM
				common.outbox_messages
			WHERE
				status = 0
				AND (next_attempt_at IS NULL OR next_attempt_at <= @now)
			ORDER BY id
			LIMIT @batch;
			""",
			(NpgsqlConnection)_session.Connection);
		command.Parameters.AddWithValue("now", DateTime.UtcNow);
		command.Parameters.AddWithValue("batch", _options.BatchSize);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);

		while (await reader.ReadAsync(cancellationToken))
		{
			candidateIds.Add(reader.GetInt64(0));
		}

		return candidateIds;
	}

	// Processa uma mensagem por transação, travando só essa linha (FOR UPDATE SKIP LOCKED) pelo
	// tempo da publicação. Isso é o que permite mais de uma instância deste processo rodar ao mesmo
	// tempo sem publicar a mesma mensagem duas vezes: quem chega primeiro trava a linha; a outra
	// instância, ao tentar a mesma linha, recebe zero registros (SKIP LOCKED) e simplesmente pula.
	private async Task<bool> TryPublishOneAsync(long id, CancellationToken cancellationToken)
	{
		var connection = (NpgsqlConnection)_session.Connection;
		await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

		var message = await LockPendingMessageAsync(connection, transaction, id, cancellationToken);

		if (message is null)
		{
			await transaction.RollbackAsync(cancellationToken);
			return false;
		}

		try
		{
			var envelope = new MessageEnvelope(
				message.ExternalId,
				message.Producer,
				message.MessageType,
				message.SchemaVersion,
				message.CreatedAt,
				message.CorrelationId,
				message.Payload
			);

			await _publisher.PublishAsync(envelope, cancellationToken);

			message.MarkAsPublished();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			message.RegisterFailedAttempt(
				ex.Message,
				DateTime.UtcNow.Add(_options.InitialRetryDelay)
			);

			_logger.LogWarning(
				ex,
				"Falha ao publicar a mensagem {MessageId}.",
				message.ExternalId
			);
		}

		await UpdateAsync(message, connection, transaction, cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return true;
	}

	private static async Task<OutboxMessage?> LockPendingMessageAsync(
		NpgsqlConnection connection,
		NpgsqlTransaction transaction,
		long id,
		CancellationToken cancellationToken)
	{
		await using var command = new NpgsqlCommand(
			"""
			SELECT
				id,
				external_id,
				created_at,
				updated_at,
				message_type,
				schema_version,
				producer,
				payload,
				correlation_id,
				status,
				published_at,
				attempt_count,
				last_attempt_at,
				next_attempt_at,
				last_error
			FROM
				common.outbox_messages
			WHERE
				id = @id
				AND status = 0
			FOR UPDATE SKIP LOCKED;
			""",
			connection,
			transaction);
		command.Parameters.AddWithValue("id", id);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);

		return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
	}

	private static async Task UpdateAsync(
		OutboxMessage message,
		NpgsqlConnection connection,
		NpgsqlTransaction transaction,
		CancellationToken cancellationToken)
	{
		await using var command = new NpgsqlCommand(
			"""
			UPDATE common.outbox_messages
			SET
				status = @status,
				updated_at = @updated_at,
				published_at = @published_at,
				attempt_count = @attempt_count,
				last_attempt_at = @last_attempt_at,
				next_attempt_at = @next_attempt_at,
				last_error = @last_error
			WHERE
				external_id = @external_id;
			""",
			connection,
			transaction);
		command.Parameters.AddWithValue("status", (int)message.Status);
		command.Parameters.AddWithValue("updated_at", DateTime.UtcNow);
		command.Parameters.AddWithValue("published_at", (object?)message.PublishedAt ?? DBNull.Value);
		command.Parameters.AddWithValue("attempt_count", message.AttemptCount);
		command.Parameters.AddWithValue("last_attempt_at", (object?)message.LastAttemptAt ?? DBNull.Value);
		command.Parameters.AddWithValue("next_attempt_at", (object?)message.NextAttemptAt ?? DBNull.Value);
		command.Parameters.AddWithValue("last_error", (object?)message.LastError ?? DBNull.Value);
		command.Parameters.AddWithValue("external_id", message.ExternalId);

		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static OutboxMessage Map(System.Data.Common.DbDataReader reader)
	{
		return OutboxMessage.Rehydrate(
			id: reader.GetInt64(reader.GetOrdinal("id")),
			externalId: reader.GetGuid(reader.GetOrdinal("external_id")),
			createdAt: reader.GetDateTime(reader.GetOrdinal("created_at")),
			updatedAt: Date(reader, "updated_at"),
			messageType: reader.GetString(reader.GetOrdinal("message_type")),
			schemaVersion: reader.GetInt32(reader.GetOrdinal("schema_version")),
			producer: reader.GetString(reader.GetOrdinal("producer")),
			payload: reader.GetString(reader.GetOrdinal("payload")),
			correlationId: Str(reader, "correlation_id"),
			status: (OutboxMessageStatus)reader.GetInt32(reader.GetOrdinal("status")),
			publishedAt: Date(reader, "published_at"),
			attemptCount: reader.GetInt32(reader.GetOrdinal("attempt_count")),
			lastAttemptAt: Date(reader, "last_attempt_at"),
			nextAttemptAt: Date(reader, "next_attempt_at"),
			lastError: Str(reader, "last_error")
		);
	}

	private static DateTime? Date(System.Data.Common.DbDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);

		if (reader.IsDBNull(ordinal))
		{
			return null;
		}

		return reader.GetDateTime(ordinal);
	}

	private static string? Str(System.Data.Common.DbDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);

		if (reader.IsDBNull(ordinal))
		{
			return null;
		}

		return reader.GetString(ordinal);
	}
}
