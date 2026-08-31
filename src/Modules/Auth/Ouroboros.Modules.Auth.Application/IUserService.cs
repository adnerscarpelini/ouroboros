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
}
