using Ouroboros.BuildingBlocks.Domain;

namespace Ouroboros.Services.Auth.Domain;

public sealed class RefreshToken : Entity
{
	public long UserId { get; private set; }
	public string TokenHash { get; private set; } = null!;
	public DateTime ExpiresAt { get; private set; }
	public DateTime? RevokedAt { get; private set; }

	// Referência navegável para a mesma coluna user_id — ver comentário equivalente em Token.
	public User User { get; private set; } = null!;

	// Construtor sem parâmetros exclusivo para o EF Core materializar a entidade a partir do banco.
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

	public void Revoke()
	{
		RevokedAt = DateTime.UtcNow;
	}
}
