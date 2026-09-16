namespace Ouroboros.AuthService.Infrastructure.Tests;

public class TokenGeneratorTests
{
	[Fact]
	public void GenerateToken_ReturnsDifferentValuesEachCall()
	{
		var tokenGenerator = new TokenGeneratorService();

		var first = tokenGenerator.GenerateToken();
		var second = tokenGenerator.GenerateToken();

		Assert.NotEqual(first, second);
	}

	[Fact]
	public void Hash_IsDeterministic()
	{
		var tokenGenerator = new TokenGeneratorService();
		var token = tokenGenerator.GenerateToken();

		Assert.Equal(tokenGenerator.Hash(token), tokenGenerator.Hash(token));
	}

	[Fact]
	public void Hash_NeverReturnsTheRawToken()
	{
		var tokenGenerator = new TokenGeneratorService();
		var token = tokenGenerator.GenerateToken();

		Assert.NotEqual(token, tokenGenerator.Hash(token));
	}
}
