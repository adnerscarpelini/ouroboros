using Ouroboros.BuildingBlocks.Domain;

namespace Ouroboros.Services.Auth.Domain;

public sealed class TokenType : Entity
{
	public string Name { get; private set; } = null!;

	// Construtor sem parâmetros usado exclusivamente pela fábrica de reidratação SQL.
	private TokenType()
	{
	}

	public TokenType(string name)
	{
		Name = name;
	}

	public static TokenType Rehydrate(
		long id,
		Guid externalId,
		DateTime createdAt,
		DateTime? updatedAt,
		string name)
	{
		var tokenType = new TokenType(name);
		tokenType.RestorePersistence(id, externalId, createdAt, updatedAt);
		return tokenType;
	}
}
