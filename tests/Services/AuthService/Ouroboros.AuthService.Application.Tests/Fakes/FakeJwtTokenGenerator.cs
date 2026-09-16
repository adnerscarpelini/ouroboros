using Ouroboros.AuthService.Domain;

namespace Ouroboros.AuthService.Application.Tests;

public sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
{
	public AccessTokenResult GenerateToken(User user)
	{
		return new AccessTokenResult($"token-for:{user.Login}", DateTime.UtcNow.AddHours(1));
	}
}
