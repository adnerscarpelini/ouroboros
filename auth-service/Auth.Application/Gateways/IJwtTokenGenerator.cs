namespace Ouroboros.Auth.Application.Gateways;

public interface IJwtTokenGenerator
{
    AccessToken Generate(
        Guid userId,
        string login,
        string email);
}
