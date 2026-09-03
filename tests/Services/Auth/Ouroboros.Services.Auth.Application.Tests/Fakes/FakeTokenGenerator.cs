namespace Ouroboros.Services.Auth.Application.Tests;

public sealed class FakeTokenGenerator : ITokenGenerator
{
	public string GenerateToken()
	{
		return "raw-token";
	}

	public string Hash(string token)
	{
		return $"hashed:{token}";
	}
}
