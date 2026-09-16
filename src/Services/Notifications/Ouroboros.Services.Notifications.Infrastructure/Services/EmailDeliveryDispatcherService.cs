using Microsoft.Extensions.Logging;
using Ouroboros.Services.Notifications.Application;
using Ouroboros.Services.Notifications.Domain;

namespace Ouroboros.Services.Notifications.Infrastructure;

// Uma passada pela fila de entregas: pega o próximo lote de entregas vencidas, tenta entregar cada uma
// e grava o resultado. Separado do EmailDeliveryProcessorService de propósito — aqui mora o "o que fazer",
// lá o "de quanto em quanto tempo". É essa separação que permite testar o despacho sem temporizador.
public sealed class EmailDeliveryDispatcherService
{
	private const int MaxBackoffExponent = 16;

	private readonly IEmailDeliveryRepository _emailDeliveryRepository;
	private readonly IEmailSender _emailSender;
	private readonly IUnitOfWork _unitOfWork;
	private readonly EmailDeliveryOptions _options;
	private readonly ILogger<EmailDeliveryDispatcherService> _logger;

	public EmailDeliveryDispatcherService(
		IEmailDeliveryRepository emailDeliveryRepository,
		IEmailSender emailSender,
		IUnitOfWork unitOfWork,
		EmailDeliveryOptions options,
		ILogger<EmailDeliveryDispatcherService> logger
	)
	{
		_emailDeliveryRepository = emailDeliveryRepository;
		_emailSender = emailSender;
		_unitOfWork = unitOfWork;
		_options = options;
		_logger = logger;
	}

	// Devolve quantas entregas foram processadas nesta passada.
	public async Task<int> DispatchDueAsync(CancellationToken cancellationToken)
	{
		var now = DateTime.UtcNow;

		var dueDeliveries = await _emailDeliveryRepository.GetDueForDeliveryAsync(
			instant: now,
			batchSize: _options.BatchSize,
			cancellationToken: cancellationToken
		);

		if (dueDeliveries.Count == 0)
		{
			return 0;
		}

		foreach (var delivery in dueDeliveries)
		{
			await DispatchOneAsync(
				delivery: delivery,
				cancellationToken: cancellationToken
			);
			_emailDeliveryRepository.Update(delivery);
		}

		await _unitOfWork.SaveChangesAsync(cancellationToken);

		return dueDeliveries.Count;
	}

	private async Task DispatchOneAsync(
		EmailDelivery delivery,
		CancellationToken cancellationToken
	)
	{
		// A validade é reconferida a cada tentativa, inclusive depois de uma espera longa ou de um
		// restart: entregar um link já vencido só produz frustração na caixa de entrada.
		if (delivery.IsExpiredAt(DateTime.UtcNow))
		{
			delivery.MarkAsExpired();

			_logger.LogWarning(
				"Entrega {RequestId} ({Producer}) expirou antes de ser enviada.",
				delivery.RequestId,
				delivery.Producer
			);

			return;
		}

		// A tentativa é registrada antes da chamada externa: se o processo cair no meio do envio, fica
		// o rastro de que ela começou, mesmo sem resultado conhecido.
		var attempt = delivery.StartAttempt();

		try
		{
			await _emailSender.SendAsync(
				recipient: delivery.Recipient,
				subject: delivery.Subject,
				bodyHtml: delivery.BodyHtml,
				cancellationToken: cancellationToken
			);

			delivery.MarkAsAccepted(attempt);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			// Catch justificado: a falha de uma entrega não pode abortar o lote inteiro. O erro é
			// gravado na própria linha e a entrega volta mais tarde, até esgotar MaxAttempts.
			if (delivery.HasExhaustedAttempts(_options.MaxAttempts))
			{
				delivery.MarkAsFailed(
					attempt: attempt,
					error: exception.Message
				);

				_logger.LogError(
					exception,
					"Entrega {RequestId} ({Producer}) esgotou as {MaxAttempts} tentativas.",
					delivery.RequestId,
					delivery.Producer,
					_options.MaxAttempts
				);

				return;
			}

			delivery.ScheduleRetry(
				attempt: attempt,
				error: exception.Message,
				nextAttemptAt: CalculateNextAttempt(delivery.AttemptCount)
			);

			_logger.LogWarning(
				exception,
				"Falha ao entregar {RequestId} ({Producer}). Tentativa {AttemptCount} de {MaxAttempts}.",
				delivery.RequestId,
				delivery.Producer,
				delivery.AttemptCount,
				_options.MaxAttempts
			);
		}
	}

	// Espera exponencial com jitter: sem o jitter, várias entregas que falharam na mesma rodada
	// voltariam a bater no provedor exatamente no mesmo instante.
	private DateTime CalculateNextAttempt(int attemptCount)
	{
		var exponent = Math.Clamp(attemptCount - 1, 0, MaxBackoffExponent);

		var delayTicks = _options.InitialRetryDelay.Ticks * (1L << exponent);

		var delay = delayTicks > _options.MaxRetryDelay.Ticks || delayTicks < 0
			? _options.MaxRetryDelay
			: TimeSpan.FromTicks(delayTicks);

		var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));

		return DateTime.UtcNow.Add(delay).Add(jitter);
	}
}
