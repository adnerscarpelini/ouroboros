using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ouroboros.BuildingBlocks.Application;
using Ouroboros.BuildingBlocks.Domain;

namespace Ouroboros.BuildingBlocks.Infrastructure;

// Uma passada pela fila: pega o próximo lote de mensagens não enviadas, tenta entregar cada uma e
// grava o resultado. Separado do EmailOutboxProcessor de propósito — aqui mora o "o que fazer",
// lá o "de quanto em quanto tempo". É essa separação que permite testar o despacho sem temporizador.
//
// TDbContext é o DbContext do serviço: cada serviço processa a própria fila, na própria base.
public sealed class EmailOutboxDispatcher<TDbContext>
	where TDbContext : AppDbContext
{
	private readonly TDbContext _dbContext;
	private readonly IEmailSender _emailSender;
	private readonly EmailOutboxOptions _options;
	private readonly ILogger<EmailOutboxDispatcher<TDbContext>> _logger;

	public EmailOutboxDispatcher(
		TDbContext dbContext,
		IEmailSender emailSender,
		EmailOutboxOptions options,
		ILogger<EmailOutboxDispatcher<TDbContext>> logger
	)
	{
		_dbContext = dbContext;
		_emailSender = emailSender;
		_options = options;
		_logger = logger;
	}

	// Devolve quantas mensagens foram tentadas nesta passada.
	public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken)
	{
		var pendingMessages = await _dbContext.Set<EmailMessage>()
			.Where(m => !m.Sent && m.AttemptCount < _options.MaxAttempts)
			.OrderBy(m => m.Id)
			.Take(_options.BatchSize)
			.ToListAsync(cancellationToken);

		if (pendingMessages.Count == 0)
		{
			return 0;
		}

		foreach (var pendingMessage in pendingMessages)
		{
			await SendOneAsync(
				pendingMessage: pendingMessage,
				cancellationToken: cancellationToken
			);
		}

		await _dbContext.SaveChangesAsync(cancellationToken);

		return pendingMessages.Count;
	}

	private async Task SendOneAsync(
		EmailMessage pendingMessage,
		CancellationToken cancellationToken
	)
	{
		try
		{
			await _emailSender.SendAsync(
				recipient: pendingMessage.Recipient,
				subject: pendingMessage.Subject,
				bodyHtml: pendingMessage.BodyHtml,
				cancellationToken: cancellationToken
			);

			pendingMessage.MarkAsSent();
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			// Catch justificado: a falha de uma mensagem não pode abortar o lote inteiro. O erro é
			// gravado na própria linha e a mensagem volta na próxima rodada, até esgotar MaxAttempts.
			pendingMessage.RegisterFailedAttempt(exception.Message);

			_logger.LogWarning(
				exception,
				"Falha ao enviar o e-mail {EmailMessageId} (tentativa {AttemptCount} de {MaxAttempts}).",
				pendingMessage.Id,
				pendingMessage.AttemptCount,
				_options.MaxAttempts
			);
		}
	}
}
