namespace Ouroboros.BuildingBlocks.Application;

public sealed record EmailOutboxOptions(
	string SmtpHost,
	int SmtpPort,
	string FromAddress,
	string FromName,
	// De quanto em quanto tempo a fila é varrida.
	TimeSpan PollingInterval,
	// Quantas mensagens são processadas por rodada.
	int BatchSize,
	// Depois disso a mensagem para de ser tentada — ver EmailMessage.HasExhaustedAttempts.
	int MaxAttempts
);
