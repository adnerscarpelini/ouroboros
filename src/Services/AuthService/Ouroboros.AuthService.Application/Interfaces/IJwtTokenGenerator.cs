using Ouroboros.AuthService.Domain;

namespace Ouroboros.AuthService.Application;

public interface IJwtTokenGenerator
{
	AccessTokenResult GenerateToken(User user);
}
