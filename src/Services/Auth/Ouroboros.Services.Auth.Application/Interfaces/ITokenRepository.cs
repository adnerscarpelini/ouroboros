using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Application;

public interface ITokenRepository
{
	// Só marca o token para inclusão — a gravação em si acontece no IUnitOfWork do caso de uso.
	void Add(Token token);

	// Traz o TokenType e o User junto: o caso de uso precisa dos dois para validar o token,
	// e sem isso as referências navegáveis viriam nulas.
	Task<Token?> GetByHashAsync(
		string tokenHash,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);

	Task<IReadOnlyCollection<Token>> GetPendingByUserAndTypeAsync(
		User user,
		string tokenTypeName,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);
}
