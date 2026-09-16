using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.AuthService.Application;

public interface IRequestPasswordResetUseCase
{
	Task RequestPasswordResetAsync(
		string email,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);
}
