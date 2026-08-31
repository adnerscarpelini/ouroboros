using Ouroboros.Common.Domain;

namespace Ouroboros.Modules.Auth.Domain;

public sealed class TokenType : Entity
{
	public string Name { get; private set; } = null!;

	// Construtor sem parâmetros exclusivo para o EF Core materializar a entidade a partir do banco.
	private TokenType()
	{
	}

	public TokenType(string name)
	{
		Name = name;
	}
}
