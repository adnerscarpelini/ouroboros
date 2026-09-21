namespace Ouroboros.Auth.Application.Gateways;

public interface IPasswordHasher
{
    string Hash(string password);
}
