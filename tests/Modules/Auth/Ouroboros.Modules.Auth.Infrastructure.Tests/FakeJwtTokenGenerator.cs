using Ouroboros.Modules.Auth.Application;
using Ouroboros.Modules.Auth.Domain;

namespace Ouroboros.Modules.Auth.Infrastructure.Tests;

public sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
{
	public AccessTokenResult GenerateToken(User user)
	{
		return new AccessTokenResult($"token-for:{user.Login}", DateTime.UtcNow.AddHours(1));
	}
}
