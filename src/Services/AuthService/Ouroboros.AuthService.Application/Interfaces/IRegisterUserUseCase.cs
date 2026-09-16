using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.AuthService.Application;

public interface IRegisterUserUseCase
{
	Task<Result<Guid>> RegisterAsync(
		string login,
		string fullName,
		string email,
		string password,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);
}
