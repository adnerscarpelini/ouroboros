using Isopoh.Cryptography.Argon2;
using Ouroboros.AuthService.Application;

namespace Ouroboros.AuthService.Infrastructure;

public sealed class Argon2PasswordHasherService : IPasswordHasher
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
