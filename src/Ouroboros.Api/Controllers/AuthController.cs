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

	[HttpPost("confirm-email")]
	public async Task<IActionResult> ConfirmEmail(
		[FromBody] ConfirmEmailRequest request,
		CancellationToken cancellationToken
	)
	{
		var result = await _userService.ConfirmEmailAsync(request.Token, cancellationToken);

		if (!result.IsSuccess)
		{
			return BadRequest(new { message = result.Error });
		}

		return NoContent();
	}

	[HttpGet("confirm-email")]
	public async Task<ContentResult> ConfirmEmailPage(
		[FromQuery] string token,
		CancellationToken cancellationToken
	)
	{
		var result = await _userService.ConfirmEmailAsync(token, cancellationToken);

		var templateName = result.IsSuccess ? "ConfirmationSuccess.html" : "ConfirmationFailure.html";
		var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", templateName);
		var html = await System.IO.File.ReadAllTextAsync(templatePath, cancellationToken);

		if (!result.IsSuccess)
		{
			html = html.Replace("{{Message}}", result.Error);
		}

		return Content(html, "text/html");
	}
}
