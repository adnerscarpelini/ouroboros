using Ouroboros.Services.Auth.Application;

namespace Ouroboros.Services.Auth.Infrastructure.Tests;

public sealed class FakePasswordHasher : IPasswordHasher
{
	public string Hash(string password)
	{
		return $"hashed:{password}";
	}

	public bool Verify(
		string passwordHash,
		string password
	)
	{
		return passwordHash == Hash(password);
	}
}
