using Ouroboros.BuildingBlocks.Domain;

namespace Ouroboros.AuthService.Domain;

public sealed class RefreshToken : Entity
{
	public long UserId { get; private set; }
	public string TokenHash { get; private set; } = null!;
	public DateTime ExpiresAt { get; private set; }
	public DateTime? RevokedAt { get; private set; }

	// Referência navegável para a mesma coluna user_id — ver comentário equivalente em Token.
	public User User { get; private set; } = null!;

	// Construtor sem parâmetros usado exclusivamente pela fábrica de reidratação SQL.
	private RefreshToken()
	{
	}

	public RefreshToken(
		User user,
		string tokenHash,
		DateTime expiresAt
	)
	{
		User = user;
		TokenHash = tokenHash;
		ExpiresAt = expiresAt;
		RevokedAt = null;
	}

	public static RefreshToken Rehydrate(
		long id,
		Guid externalId,
		DateTime createdAt,
		DateTime? updatedAt,
		long userId,
		User user,
		string tokenHash,
		DateTime expiresAt,
		DateTime? revokedAt)
	{
		var refreshToken = new RefreshToken(user, tokenHash, expiresAt)
		{
			UserId = userId,
			RevokedAt = revokedAt
		};

		refreshToken.RestorePersistence(id, externalId, createdAt, updatedAt);
		return refreshToken;
	}

	public void Revoke()
	{
		RevokedAt = DateTime.UtcNow;
	}
}
