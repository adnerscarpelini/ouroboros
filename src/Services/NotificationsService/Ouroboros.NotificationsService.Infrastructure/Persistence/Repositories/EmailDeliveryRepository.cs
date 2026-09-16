using Npgsql;
using Ouroboros.BuildingBlocks.Infrastructure;
using Ouroboros.NotificationsService.Application;
using Ouroboros.NotificationsService.Domain;

namespace Ouroboros.NotificationsService.Infrastructure;

public sealed class EmailDeliveryRepository : IEmailDeliveryRepository
{
	private readonly DbSession _session;

	public EmailDeliveryRepository(DbSession session)
	{
		_session = session;
	}

	public void Add(EmailDelivery delivery)
		=> _session.Enqueue(async (connection, transaction, cancellationToken) =>
		{
			await using var command = new NpgsqlCommand(
				"""
				INSERT INTO notifications.email_deliveries (
					external_id,
					created_at,
					updated_at,
					producer,
					request_id,
					recipient,
					template_key,
					template_version,
					locale,
					subject,
					body_html,
					content_fingerprint,
					expires_at,
					correlation_id,
					status,
					attempt_count,
					next_attempt_at,
					sent_at,
					last_error
				)
				VALUES (
					@external_id,
					@created_at,
					NULL,
					@producer,
					@request_id,
					@recipient,
					@template_key,
					@template_version,
					@locale,
					@subject,
					@body_html,
					@content_fingerprint,
					@expires_at,
					@correlation_id,
					@status,
					0,
					NULL,
					NULL,
					NULL
				);
				""",
				(NpgsqlConnection)connection,
				(NpgsqlTransaction)transaction);

			command.Parameters.AddWithValue("external_id", delivery.ExternalId);
			command.Parameters.AddWithValue("created_at", delivery.CreatedAt);
			command.Parameters.AddWithValue("producer", delivery.Producer);
			command.Parameters.AddWithValue("request_id", delivery.RequestId);
			command.Parameters.AddWithValue("recipient", delivery.Recipient);
			command.Parameters.AddWithValue("template_key", delivery.TemplateKey);
			command.Parameters.AddWithValue("template_version", delivery.TemplateVersion);
			command.Parameters.AddWithValue("locale", delivery.Locale);
			command.Parameters.AddWithValue("subject", delivery.Subject);
			command.Parameters.AddWithValue("body_html", delivery.BodyHtml);
			command.Parameters.AddWithValue("content_fingerprint", delivery.ContentFingerprint);
			command.Parameters.AddWithValue("expires_at", delivery.ExpiresAt);
			command.Parameters.AddWithValue("correlation_id", (object?)delivery.CorrelationId ?? DBNull.Value);
			command.Parameters.AddWithValue("status", (int)delivery.Status);

			await command.ExecuteNonQueryAsync(cancellationToken);
		});

	public void Update(EmailDelivery delivery)
		=> _session.Enqueue(async (connection, transaction, cancellationToken) =>
		{
			await using var command = new NpgsqlCommand(
				"""
				UPDATE notifications.email_deliveries
				SET
					updated_at = @updated_at,
					status = @status,
					attempt_count = @attempt_count,
					next_attempt_at = @next_attempt_at,
					sent_at = @sent_at,
					last_error = @last_error
				WHERE
					external_id = @external_id;
				""",
				(NpgsqlConnection)connection,
				(NpgsqlTransaction)transaction);

			command.Parameters.AddWithValue("updated_at", DateTime.UtcNow);
			command.Parameters.AddWithValue("status", (int)delivery.Status);
			command.Parameters.AddWithValue("attempt_count", delivery.AttemptCount);
			command.Parameters.AddWithValue("next_attempt_at", (object?)delivery.NextAttemptAt ?? DBNull.Value);
			command.Parameters.AddWithValue("sent_at", (object?)delivery.SentAt ?? DBNull.Value);
			command.Parameters.AddWithValue("last_error", (object?)delivery.LastError ?? DBNull.Value);
			command.Parameters.AddWithValue("external_id", delivery.ExternalId);

			if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
			{
				throw new InvalidOperationException("Entrega não encontrada.");
			}
		});

	public async Task<EmailDelivery?> GetByRequestAsync(
		string producer,
		Guid requestId,
		CancellationToken cancellationToken)
	{
		await _session.OpenAsync(cancellationToken);
		await using var command = new NpgsqlCommand(
			"""
			SELECT
				id,
				external_id,
				created_at,
				updated_at,
				producer,
				request_id,
				recipient,
				template_key,
				template_version,
				locale,
				subject,
				body_html,
				content_fingerprint,
				expires_at,
				correlation_id,
				status,
				attempt_count,
				next_attempt_at,
				sent_at,
				last_error
			FROM
				notifications.email_deliveries
			WHERE
				producer = @producer
				AND request_id = @request_id
			LIMIT 1;
			""",
			(NpgsqlConnection)_session.Connection);
		command.Parameters.AddWithValue("producer", producer);
		command.Parameters.AddWithValue("request_id", requestId);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);

		if (!await reader.ReadAsync(cancellationToken))
		{
			return null;
		}

		return Map(reader);
	}

	public async Task<IReadOnlyCollection<EmailDelivery>> GetDueForDeliveryAsync(
		DateTime instant,
		int batchSize,
		CancellationToken cancellationToken)
	{
		await _session.OpenAsync(cancellationToken);
		var deliveries = new List<EmailDelivery>();
		await using var command = new NpgsqlCommand(
			"""
			SELECT
				id,
				external_id,
				created_at,
				updated_at,
				producer,
				request_id,
				recipient,
				template_key,
				template_version,
				locale,
				subject,
				body_html,
				content_fingerprint,
				expires_at,
				correlation_id,
				status,
				attempt_count,
				next_attempt_at,
				sent_at,
				last_error
			FROM
				notifications.email_deliveries
			WHERE
				status IN (0, 1)
				AND (next_attempt_at IS NULL OR next_attempt_at <= @instant)
			ORDER BY id
			LIMIT @batch;
			""",
			(NpgsqlConnection)_session.Connection);
		command.Parameters.AddWithValue("instant", instant);
		command.Parameters.AddWithValue("batch", batchSize);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);

		while (await reader.ReadAsync(cancellationToken))
		{
			deliveries.Add(Map(reader));
		}

		return deliveries;
	}

	private static EmailDelivery Map(System.Data.Common.DbDataReader reader)
	{
		static DateTime? GetDate(System.Data.Common.DbDataReader item, string column)
		{
			var ordinal = item.GetOrdinal(column);

			if (item.IsDBNull(ordinal))
			{
				return null;
			}

			return item.GetDateTime(ordinal);
		}

		static string? GetString(System.Data.Common.DbDataReader item, string column)
		{
			var ordinal = item.GetOrdinal(column);

			if (item.IsDBNull(ordinal))
			{
				return null;
			}

			return item.GetString(ordinal);
		}

		return EmailDelivery.Rehydrate(
			id: reader.GetInt64(reader.GetOrdinal("id")),
			externalId: reader.GetGuid(reader.GetOrdinal("external_id")),
			createdAt: reader.GetDateTime(reader.GetOrdinal("created_at")),
			updatedAt: GetDate(reader, "updated_at"),
			producer: reader.GetString(reader.GetOrdinal("producer")),
			requestId: reader.GetGuid(reader.GetOrdinal("request_id")),
			recipient: reader.GetString(reader.GetOrdinal("recipient")),
			templateKey: reader.GetString(reader.GetOrdinal("template_key")),
			templateVersion: reader.GetInt32(reader.GetOrdinal("template_version")),
			locale: reader.GetString(reader.GetOrdinal("locale")),
			subject: reader.GetString(reader.GetOrdinal("subject")),
			bodyHtml: reader.GetString(reader.GetOrdinal("body_html")),
			contentFingerprint: reader.GetString(reader.GetOrdinal("content_fingerprint")),
			expiresAt: reader.GetDateTime(reader.GetOrdinal("expires_at")),
			correlationId: GetString(reader, "correlation_id"),
			status: (EmailDeliveryStatus)reader.GetInt32(reader.GetOrdinal("status")),
			attemptCount: reader.GetInt32(reader.GetOrdinal("attempt_count")),
			nextAttemptAt: GetDate(reader, "next_attempt_at"),
			sentAt: GetDate(reader, "sent_at"),
			lastError: GetString(reader, "last_error")
		);
	}
}
