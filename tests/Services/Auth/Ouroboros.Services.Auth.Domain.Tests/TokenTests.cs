namespace Ouroboros.Services.Auth.Domain.Tests;

public class TokenTests
{
	private static Token CreateToken()
	{
		return new Token(
			tokenType: new TokenType(TokenTypeNames.UserCreationValidation),
			user: new User(
				login: "jsilva",
				fullName: "João Silva",
				email: "joao.silva@example.com",
				passwordHash: "hashed:existing"
			),
			emailMessageId: 1,
			tokenHash: "hash",
			expiresAt: DateTime.UtcNow.AddHours(1)
		);
	}

	[Fact]
	public void Constructor_CreatesUnvalidatedToken()
	{
		var token = CreateToken();

		Assert.False(token.Validated);
		Assert.Null(token.ValidatedAt);
	}

	[Fact]
	public void Constructor_KeepsTokenTypeAndUserReferences()
	{
		var token = CreateToken();

		Assert.Equal(TokenTypeNames.UserCreationValidation, token.TokenType.Name);
		Assert.Equal("jsilva", token.User.Login);
	}

	[Fact]
	public void Validate_SetsValidatedAndValidatedAt()
	{
		var token = CreateToken();

		token.Validate();

		Assert.True(token.Validated);
		Assert.NotNull(token.ValidatedAt);
	}
}
