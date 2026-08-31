using Ouroboros.Modules.Auth.Application;
using Ouroboros.Modules.Auth.Domain;

namespace Ouroboros.Modules.Auth.Infrastructure.Tests;

public sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
{
	public AuthenticationResult GenerateToken(User user)
	{
		return new AuthenticationResult($"token-for:{user.Login}", DateTime.UtcNow.AddHours(1));
	}
}
