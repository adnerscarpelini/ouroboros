namespace Ouroboros.BuildingBlocks.Application;

public sealed record OutboxOptions(
	// De quanto em quanto tempo a outbox é varrida em busca de mensagens pendentes.
	TimeSpan PollingInterval,
	// Quantas mensagens são publicadas por rodada.
	int BatchSize,
	// Espera antes da primeira retentativa. Dobra a cada falha, até MaxRetryDelay.
	TimeSpan InitialRetryDelay,
	TimeSpan MaxRetryDelay
);
