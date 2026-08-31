using Microsoft.AspNetCore.Mvc;
using Ouroboros.Api.Contracts.Auth;
using Ouroboros.Modules.Auth.Application;

namespace Ouroboros.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
	private readonly IUserService _userService;

	public AuthController(IUserService userService)
	{
		_userService = userService;
	}

	[HttpPost("register")]
	public async Task<IActionResult> Register(
		[FromBody] RegisterUserRequest request,
		CancellationToken cancellationToken
	)
	{
		var result = await _userService.CreateUserAsync(
			login: request.Login,
			fullName: request.FullName,
			email: request.Email,
			password: request.Password,
			cancellationToken: cancellationToken
		);

		if (!result.IsSuccess)
		{
			return Conflict(new { message = result.Error });
		}

		return StatusCode(StatusCodes.Status201Created, new RegisterUserResponse(result.Value));
	}
}
