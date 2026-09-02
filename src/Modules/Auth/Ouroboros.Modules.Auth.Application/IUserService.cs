using Ouroboros.Common.Application;

namespace Ouroboros.Modules.Auth.Application;

public interface IUserService
{
	Task<Result<Guid>> CreateUserAsync(
		string login,
		string fullName,
		string email,
		string password,
		CancellationToken cancellationToken
	);

	Task<Result> ConfirmEmailAsync(
		string token,
		CancellationToken cancellationToken
	);

	Task<Result<AuthenticationResult>> LoginAsync(
		string login,
		string password,
		CancellationToken cancellationToken
	);

	Task RequestPasswordResetAsync(
		string email,
		CancellationToken cancellationToken
	);

	Task<Result> ResetPasswordAsync(
		string token,
		string newPassword,
		CancellationToken cancellationToken
	);

	Task<Result<AuthenticationResult>> RefreshTokenAsync(
		string refreshToken,
		CancellationToken cancellationToken
	);

	Task<Result> LogoutAsync(
		string refreshToken,
		CancellationToken cancellationToken
	);
}
