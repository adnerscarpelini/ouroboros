using Ouroboros.Common.Domain;

namespace Ouroboros.Modules.Auth.Domain;

public sealed class RefreshToken : Entity
{
	public long UserId { get; private set; }
	public string TokenHash { get; private set; } = null!;
	public DateTime ExpiresAt { get; private set; }
	public DateTime? RevokedAt { get; private set; }

	// Construtor sem parâmetros exclusivo para o EF Core materializar a entidade a partir do banco.
	private RefreshToken()
	{
	}

	public RefreshToken(
		long userId,
		string tokenHash,
		DateTime expiresAt
	)
	{
		UserId = userId;
		TokenHash = tokenHash;
		ExpiresAt = expiresAt;
		RevokedAt = null;
	}

	public void Revoke()
	{
		RevokedAt = DateTime.UtcNow;
	}
}
