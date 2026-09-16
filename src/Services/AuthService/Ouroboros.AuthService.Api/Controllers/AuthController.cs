using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ouroboros.AuthService.Api.Contracts.Auth;
using Ouroboros.AuthService.Application;

namespace Ouroboros.AuthService.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
	private readonly IRegisterUserUseCase _registerUserUseCase;
	private readonly IConfirmEmailUseCase _confirmEmailUseCase;
	private readonly ILoginUseCase _loginUseCase;
	private readonly IRefreshTokenUseCase _refreshTokenUseCase;
	private readonly ILogoutUseCase _logoutUseCase;
	private readonly IRequestPasswordResetUseCase _requestPasswordResetUseCase;
	private readonly IResetPasswordUseCase _resetPasswordUseCase;

	public AuthController(
		IRegisterUserUseCase registerUserUseCase,
		IConfirmEmailUseCase confirmEmailUseCase,
		ILoginUseCase loginUseCase,
		IRefreshTokenUseCase refreshTokenUseCase,
		ILogoutUseCase logoutUseCase,
		IRequestPasswordResetUseCase requestPasswordResetUseCase,
		IResetPasswordUseCase resetPasswordUseCase
	)
	{
		_registerUserUseCase = registerUserUseCase;
		_confirmEmailUseCase = confirmEmailUseCase;
		_loginUseCase = loginUseCase;
		_refreshTokenUseCase = refreshTokenUseCase;
		_logoutUseCase = logoutUseCase;
		_requestPasswordResetUseCase = requestPasswordResetUseCase;
		_resetPasswordUseCase = resetPasswordUseCase;
	}

	[AllowAnonymous]
	[HttpPost("register")]
	public async Task<IActionResult> Register(
		[FromBody] RegisterUserRequest request,
		CancellationToken cancellationToken
	)
	{
		var result = await _registerUserUseCase.RegisterAsync(
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
		var result = await _confirmEmailUseCase.ConfirmEmailAsync(
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
		var result = await _loginUseCase.LoginAsync(
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
		var result = await _refreshTokenUseCase.RefreshTokenAsync(
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
		var result = await _logoutUseCase.LogoutAsync(
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
		await _requestPasswordResetUseCase.RequestPasswordResetAsync(
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
		var result = await _resetPasswordUseCase.ResetPasswordAsync(
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
		var result = await _confirmEmailUseCase.ConfirmEmailAsync(
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
