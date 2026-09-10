namespace Ouroboros.BuildingBlocks.Application;

// O X-Correlation-Id da requisição sendo atendida, quando existe uma. Devolve null fora de um
// request HTTP (processador de fundo, por exemplo), e quem consome precisa aceitar isso.
public interface ICorrelationIdAccessor
{
	string? GetCorrelationId();
}
