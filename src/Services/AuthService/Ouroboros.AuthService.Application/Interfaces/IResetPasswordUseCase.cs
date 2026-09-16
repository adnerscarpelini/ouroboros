using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.AuthService.Application;

public interface IResetPasswordUseCase
{
	Task<Result> ResetPasswordAsync(
		string token,
		string newPassword,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);
}
