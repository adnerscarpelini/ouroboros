using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Application;

public interface ITokenTypeRepository
{
	// Os tipos de token são dados de seed da migration, não algo criado em runtime — por isso a
	// ausência de um nome conhecido é falha de configuração do banco, não um caso de negócio.
	Task<TokenType> GetByNameAsync(
		string name,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);
}
