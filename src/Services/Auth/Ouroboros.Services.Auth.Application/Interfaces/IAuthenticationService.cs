using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.Services.Auth.Application;

public interface IAuthenticationService
{
	Task<Result<AuthenticationResult>> LoginAsync(
		string login,
		string password,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);

	Task<Result<AuthenticationResult>> RefreshTokenAsync(
		string refreshToken,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);

	Task<Result> LogoutAsync(
		string refreshToken,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);
}
