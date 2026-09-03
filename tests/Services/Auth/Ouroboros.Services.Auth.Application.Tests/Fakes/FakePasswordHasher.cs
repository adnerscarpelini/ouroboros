namespace Ouroboros.Services.Auth.Application.Tests;

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
