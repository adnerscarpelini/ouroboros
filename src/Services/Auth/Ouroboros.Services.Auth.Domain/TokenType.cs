using Ouroboros.BuildingBlocks.Domain;

namespace Ouroboros.Services.Auth.Domain;

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
