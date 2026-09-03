namespace Ouroboros.Services.Auth.Infrastructure.Tests;

public class TokenGeneratorTests
{
	[Fact]
	public void GenerateToken_ReturnsDifferentValuesEachCall()
	{
		var tokenGenerator = new TokenGenerator();

		var first = tokenGenerator.GenerateToken();
		var second = tokenGenerator.GenerateToken();

		Assert.NotEqual(first, second);
	}

	[Fact]
	public void Hash_IsDeterministic()
	{
		var tokenGenerator = new TokenGenerator();
		var token = tokenGenerator.GenerateToken();

		Assert.Equal(tokenGenerator.Hash(token), tokenGenerator.Hash(token));
	}

	[Fact]
	public void Hash_NeverReturnsTheRawToken()
	{
		var tokenGenerator = new TokenGenerator();
		var token = tokenGenerator.GenerateToken();

		Assert.NotEqual(token, tokenGenerator.Hash(token));
	}
}
