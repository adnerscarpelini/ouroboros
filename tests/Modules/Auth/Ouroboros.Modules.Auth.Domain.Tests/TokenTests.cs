namespace Ouroboros.Modules.Auth.Domain.Tests;

public class TokenTests
{
	private static Token CreateToken()
	{
		return new Token(
			tokenTypeId: 1,
			userId: 1,
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
	public void Validate_SetsValidatedAndValidatedAt()
	{
		var token = CreateToken();

		token.Validate();

		Assert.True(token.Validated);
		Assert.NotNull(token.ValidatedAt);
	}
}
