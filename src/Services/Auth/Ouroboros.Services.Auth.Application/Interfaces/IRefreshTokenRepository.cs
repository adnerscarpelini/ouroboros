using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Application;

public interface IRefreshTokenRepository
{
	// Só marca o token para inclusão — a gravação em si acontece no IUnitOfWork do caso de uso.
	void Add(RefreshToken refreshToken);

	// Traz o User junto: o caso de uso emite um novo par de tokens para ele na rotação.
	Task<RefreshToken?> GetByHashAsync(
		string tokenHash,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);
}
