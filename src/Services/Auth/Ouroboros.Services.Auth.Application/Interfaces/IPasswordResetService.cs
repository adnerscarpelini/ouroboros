using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.Services.Auth.Application;

public interface IPasswordResetService
{
	Task RequestPasswordResetAsync(
		string email,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);

	Task<Result> ResetPasswordAsync(
		string token,
		string newPassword,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);
}
