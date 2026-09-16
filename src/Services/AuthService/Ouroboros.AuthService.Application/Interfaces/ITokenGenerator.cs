namespace Ouroboros.AuthService.Application;

public interface ITokenGenerator
{
	string GenerateToken();

	string Hash(string token);
}
