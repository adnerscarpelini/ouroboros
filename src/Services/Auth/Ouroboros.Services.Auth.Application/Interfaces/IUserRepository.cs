using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Application;

public interface IUserRepository
{
	// Só marca o usuário para inclusão — a gravação em si acontece no IUnitOfWork do caso de uso.
	void Add(User user);

	Task<User?> GetByLoginAsync(
		string login,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);

	Task<User?> GetByEmailAsync(
		string email,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);

	Task<bool> ExistsByLoginAsync(
		string login,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);

	Task<bool> ExistsByEmailAsync(
		string email,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);
}
