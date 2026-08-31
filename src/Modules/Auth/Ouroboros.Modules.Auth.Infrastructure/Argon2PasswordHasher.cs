using Isopoh.Cryptography.Argon2;
using Ouroboros.Modules.Auth.Application;

namespace Ouroboros.Modules.Auth.Infrastructure;

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
