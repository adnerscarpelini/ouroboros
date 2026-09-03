namespace Ouroboros.Services.Auth.Domain.Tests;

public class TokenTypeTests
{
	[Fact]
	public void Constructor_SetsName()
	{
		var tokenType = new TokenType(TokenTypeNames.UserCreationValidation);

		Assert.Equal(TokenTypeNames.UserCreationValidation, tokenType.Name);
	}
}
