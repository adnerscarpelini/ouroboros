using Ouroboros.Modules.Auth.Application;

namespace Ouroboros.Modules.Auth.Infrastructure.Tests;

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
