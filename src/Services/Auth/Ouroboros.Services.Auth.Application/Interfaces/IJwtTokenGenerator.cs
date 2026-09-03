using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Application;

public interface IJwtTokenGenerator
{
	AccessTokenResult GenerateToken(User user);
}
