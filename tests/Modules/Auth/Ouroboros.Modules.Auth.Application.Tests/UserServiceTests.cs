using Ouroboros.Modules.Auth.Application;

namespace Ouroboros.Modules.Auth.Application.Tests;

public class UserServiceTests
{
	[Fact]
	public void CreateUser_ReturnsTrue()
	{
		var userService = new UserService();

		var result = userService.CreateUser(
			email: "user@example.com",
			password: "any-password"
		);

		Assert.True(result);
	}
}
