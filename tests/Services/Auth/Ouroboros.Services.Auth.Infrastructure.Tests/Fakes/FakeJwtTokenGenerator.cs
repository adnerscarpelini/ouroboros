using Ouroboros.Services.Auth.Application;
using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Infrastructure.Tests;

public sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
{
	public AccessTokenResult GenerateToken(User user)
	{
		return new AccessTokenResult($"token-for:{user.Login}", DateTime.UtcNow.AddHours(1));
	}
}
