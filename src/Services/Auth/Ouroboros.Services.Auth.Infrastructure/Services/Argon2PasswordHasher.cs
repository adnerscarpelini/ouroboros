using Isopoh.Cryptography.Argon2;
using Ouroboros.Services.Auth.Application;

namespace Ouroboros.Services.Auth.Infrastructure;

public sealed class Argon2PasswordHasher : IPasswordHasher
{
	public string Hash(string password)
	{
		return Argon2.Hash(password);
	}

	public bool Verify(
		string passwordHash,
		string password
	)
	{
		return Argon2.Verify(passwordHash, password);
	}
}
