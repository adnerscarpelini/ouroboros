using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ouroboros.Services.Auth.Api.Contracts.Auth;
using Ouroboros.Services.Auth.Application;

namespace Ouroboros.Services.Auth.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
	private readonly IUserRegistrationService _userRegistrationService;
	private readonly IAuthenticationService _authenticationService;
	private readonly IPasswordResetService _passwordResetService;

	public AuthController(
		IUserRegistrationService userRegistrationService,
		IAuthenticationService authenticationService,
		IPasswordResetService passwordResetService
	)
	{
		_userRegistrationService = userRegistrationService;
		_authenticationService = authenticationService;
		_passwordResetService = passwordResetService;
	}

	[AllowAnonymous]
	[HttpPost("register")]
	public async Task<IActionResult> Register(
		[FromBody] RegisterUserRequest request,
		CancellationToken cancellationToken
	)
	{
		var result = await _userRegistrationService.CreateUserAsync(
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

	[AllowAnonymous]
	[HttpPost("confirm-email")]
	public async Task<IActionResult> ConfirmEmail(
		[FromBody] ConfirmEmailRequest request,
		CancellationToken cancellationToken
	)
	{
		var result = await _userRegistrationService.ConfirmEmailAsync(
			token: request.Token,
			cancellationToken: cancellationToken
		);

		if (!result.IsSuccess)
		{
			return BadRequest(new { message = result.Error });
		}

		return NoContent();
	}

	[AllowAnonymous]
	[HttpPost("login")]
	public async Task<IActionResult> Login(
		[FromBody] LoginRequest request,
		CancellationToken cancellationToken
	)
	{
		var result = await _authenticationService.LoginAsync(
			login: request.Login,
			password: request.Password,
			cancellationToken: cancellationToken
		);

		if (!result.IsSuccess)
		{
			return Unauthorized(new { message = result.Error });
		}

		return Ok(new LoginResponse(
			result.Value!.AccessToken,
			result.Value.ExpiresAt,
			result.Value.RefreshToken,
			result.Value.RefreshTokenExpiresAt
		));
	}

	[AllowAnonymous]
	[HttpPost("refresh-token")]
	public async Task<IActionResult> RefreshToken(
		[FromBody] RefreshTokenRequest request,
		CancellationToken cancellationToken
	)
	{
		var result = await _authenticationService.RefreshTokenAsync(
			refreshToken: request.RefreshToken,
			cancellationToken: cancellationToken
		);

		if (!result.IsSuccess)
		{
			return Unauthorized(new { message = result.Error });
		}

		return Ok(new LoginResponse(
			result.Value!.AccessToken,
			result.Value.ExpiresAt,
			result.Value.RefreshToken,
			result.Value.RefreshTokenExpiresAt
		));
	}

	[HttpPost("logout")]
	public async Task<IActionResult> Logout(
		[FromBody] LogoutRequest request,
		CancellationToken cancellationToken
	)
	{
		var result = await _authenticationService.LogoutAsync(
			refreshToken: request.RefreshToken,
			cancellationToken: cancellationToken
		);

		if (!result.IsSuccess)
		{
			return BadRequest(new { message = result.Error });
		}

		return NoContent();
	}

	[AllowAnonymous]
	[HttpPost("forgot-password")]
	public async Task<IActionResult> ForgotPassword(
		[FromBody] ForgotPasswordRequest request,
		CancellationToken cancellationToken
	)
	{
		await _passwordResetService.RequestPasswordResetAsync(
			email: request.Email,
			cancellationToken: cancellationToken
		);

		// Resposta sempre igual, exista ou não o e-mail — evita enumeração de contas.
		return NoContent();
	}

	[AllowAnonymous]
	[HttpPost("reset-password")]
	public async Task<IActionResult> ResetPassword(
		[FromBody] ResetPasswordRequest request,
		CancellationToken cancellationToken
	)
	{
		var result = await _passwordResetService.ResetPasswordAsync(
			token: request.Token,
			newPassword: request.NewPassword,
			cancellationToken: cancellationToken
		);

		if (!result.IsSuccess)
		{
			return BadRequest(new { message = result.Error });
		}

		return NoContent();
	}

	[AllowAnonymous]
	[HttpGet("confirm-email")]
	public async Task<ContentResult> ConfirmEmailPage(
		[FromQuery] string token,
		CancellationToken cancellationToken
	)
	{
		var result = await _userRegistrationService.ConfirmEmailAsync(
			token: token,
			cancellationToken: cancellationToken
		);

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
