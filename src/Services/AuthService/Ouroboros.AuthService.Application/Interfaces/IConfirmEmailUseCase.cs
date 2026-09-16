using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.AuthService.Application;

public interface IConfirmEmailUseCase
{
	Task<Result> ConfirmEmailAsync(
		string token,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);
}
