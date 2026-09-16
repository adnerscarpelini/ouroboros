using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.AuthService.Application;

public interface ILogoutUseCase
{
	Task<Result> LogoutAsync(
		string refreshToken,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);
}
