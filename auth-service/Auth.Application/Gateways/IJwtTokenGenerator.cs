namespace Ouroboros.Auth.Application.Gateways;

using Ouroboros.Auth.Domain.Entities;

public interface IJwtTokenGenerator
{
    AccessToken Generate(
        Guid userId,
        string login,
        string email,
        UserRole role);
}
