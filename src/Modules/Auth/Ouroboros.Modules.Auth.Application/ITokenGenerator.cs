namespace Ouroboros.Modules.Auth.Application;

public interface ITokenGenerator
{
	string GenerateToken();

	string Hash(string token);
}
