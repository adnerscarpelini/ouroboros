namespace Ouroboros.Modules.Auth.Application;

public interface IPasswordHasher
{
	string Hash(string password);

	bool Verify(
		string passwordHash,
		string password
	);
}
