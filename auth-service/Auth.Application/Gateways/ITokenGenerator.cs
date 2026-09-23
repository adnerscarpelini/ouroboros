namespace Ouroboros.Auth.Application.Gateways;

public interface ITokenGenerator
{
    string Generate();

    string Hash(string token);
}
