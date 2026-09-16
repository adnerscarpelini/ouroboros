using Ouroboros.BuildingBlocks.Domain;

namespace Ouroboros.Services.Auth.Domain;

public sealed class Token : Entity
{
	public long TokenTypeId { get; private set; }
	public long UserId { get; private set; }
	// Identificador da solicitação de notificação que leva este token ao usuário. É um Guid solto de
	// propósito: a entrega vive no banco do serviço de Notificações, e chave estrangeira não atravessa
	// serviço. O token também sobrevive à limpeza da outbox, então nem uma FK local caberia aqui.
	public Guid NotificationRequestId { get; private set; }
	public string TokenHash { get; private set; } = null!;
	public DateTime ExpiresAt { get; private set; }
	public bool Validated { get; private set; }
	public DateTime? ValidatedAt { get; private set; }

	// Referências navegáveis para as mesmas colunas token_type_id/user_id: o caso de uso passa a
	// trabalhar com o TokenType e o User em si, não com um id que só existe depois de gravar.
	public TokenType TokenType { get; private set; } = null!;
	public User User { get; private set; } = null!;

	// Construtor sem parâmetros usado exclusivamente pela fábrica de reidratação SQL.
	private Token()
	{
	}

	public Token(
		TokenType tokenType,
		User user,
		Guid notificationRequestId,
		string tokenHash,
		DateTime expiresAt
	)
	{
		TokenType = tokenType;
		User = user;
		NotificationRequestId = notificationRequestId;
		TokenHash = tokenHash;
		ExpiresAt = expiresAt;
		Validated = false;
		ValidatedAt = null;
	}

	public static Token Rehydrate(
		long id,
		Guid externalId,
		DateTime createdAt,
		DateTime? updatedAt,
		long tokenTypeId,
		long userId,
		TokenType tokenType,
		User user,
		Guid notificationRequestId,
		string tokenHash,
		DateTime expiresAt,
		bool validated,
		DateTime? validatedAt)
	{
		var token = new Token(tokenType, user, notificationRequestId, tokenHash, expiresAt)
		{
			TokenTypeId = tokenTypeId,
			UserId = userId,
			Validated = validated,
			ValidatedAt = validatedAt
		};

		token.RestorePersistence(id, externalId, createdAt, updatedAt);
		return token;
	}

	public void Validate()
	{
		Validated = true;
		ValidatedAt = DateTime.UtcNow;
	}
}
