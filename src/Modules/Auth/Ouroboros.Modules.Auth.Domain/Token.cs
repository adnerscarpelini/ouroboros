using Ouroboros.Common.Domain;

namespace Ouroboros.Modules.Auth.Domain;

public sealed class Token : Entity
{
	public long TokenTypeId { get; private set; }
	public long UserId { get; private set; }
	public long EmailMessageId { get; private set; }
	public string TokenHash { get; private set; } = null!;
	public DateTime ExpiresAt { get; private set; }
	public bool Validated { get; private set; }
	public DateTime? ValidatedAt { get; private set; }

	// Construtor sem parâmetros exclusivo para o EF Core materializar a entidade a partir do banco.
	private Token()
	{
	}

	public Token(
		long tokenTypeId,
		long userId,
		long emailMessageId,
		string tokenHash,
		DateTime expiresAt
	)
	{
		TokenTypeId = tokenTypeId;
		UserId = userId;
		EmailMessageId = emailMessageId;
		TokenHash = tokenHash;
		ExpiresAt = expiresAt;
		Validated = false;
		ValidatedAt = null;
	}

	public void Validate()
	{
		Validated = true;
		ValidatedAt = DateTime.UtcNow;
	}
}
