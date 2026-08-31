using Ouroboros.Modules.Auth.Domain;

namespace Ouroboros.Modules.Auth.Application;

public interface IJwtTokenGenerator
{
	AuthenticationResult GenerateToken(User user);
}
