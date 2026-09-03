namespace Ouroboros.Services.Auth.Application;

public interface ITokenGenerator
{
	string GenerateToken();

	string Hash(string token);
}
