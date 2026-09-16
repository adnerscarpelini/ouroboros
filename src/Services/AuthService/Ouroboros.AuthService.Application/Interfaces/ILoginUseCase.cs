using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.AuthService.Application;

public interface ILoginUseCase
{
	Task<Result<AuthenticationResult>> LoginAsync(
		string login,
		string password,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);
}
