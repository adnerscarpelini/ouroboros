namespace Ouroboros.Services.Notifications.Application;

public sealed record EmailDeliveryOptions(
	// De quanto em quanto tempo a fila de entregas é varrida.
	TimeSpan PollingInterval,
	// Quantas entregas são processadas por rodada.
	int BatchSize,
	// Depois disso a entrega para de ser tentada e fica como Failed, com o último erro, para inspeção.
	int MaxAttempts,
	// Espera antes da primeira retentativa. Dobra a cada falha, até MaxRetryDelay.
	TimeSpan InitialRetryDelay,
	TimeSpan MaxRetryDelay
);
