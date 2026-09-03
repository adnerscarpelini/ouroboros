using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.Services.Auth.Application;

public interface IUserRegistrationService
{
	Task<Result<Guid>> CreateUserAsync(
		string login,
		string fullName,
		string email,
		string password,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);

	Task<Result> ConfirmEmailAsync(
		string token,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);
}
