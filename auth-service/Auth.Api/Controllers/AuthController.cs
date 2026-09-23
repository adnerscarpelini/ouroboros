namespace Ouroboros.Auth.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Ouroboros.Auth.Application.UseCases.Login;
using Ouroboros.Auth.Application.UseCases.Logout;
using Ouroboros.Auth.Application.UseCases.RefreshAccessToken;
using Ouroboros.Auth.Domain.Exceptions;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ILoginUseCase _loginUseCase;
    private readonly IRefreshAccessTokenUseCase _refreshAccessTokenUseCase;
    private readonly ILogoutUseCase _logoutUseCase;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ILoginUseCase loginUseCase,
        IRefreshAccessTokenUseCase refreshAccessTokenUseCase,
        ILogoutUseCase logoutUseCase,
        ILogger<AuthController> logger)
    {
        _loginUseCase = loginUseCase;
        _refreshAccessTokenUseCase = refreshAccessTokenUseCase;
        _logoutUseCase = logoutUseCase;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var response = await _loginUseCase.ExecuteAsync(request);
            return Ok(response);
        }
        catch (InvalidCredentialsException e)
        {
            _logger.LogWarning(e, "Login rejected for {Login}: {Reason}", request.Login, e.Message);
            return Unauthorized(new { error = e.Message });
        }
        catch (DomainException e)
        {
            _logger.LogWarning(e, "Login rejected for {Login}: {Reason}", request.Login, e.Message);
            return BadRequest(new { error = e.Message });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshAccessTokenRequest request)
    {
        try
        {
            var response = await _refreshAccessTokenUseCase.ExecuteAsync(request);
            return Ok(response);
        }
        catch (InvalidRefreshTokenException e)
        {
            _logger.LogWarning(e, "Token refresh rejected: {Reason}", e.Message);
            return Unauthorized(new { error = e.Message });
        }
        catch (DomainException e)
        {
            _logger.LogWarning(e, "Token refresh rejected: {Reason}", e.Message);
            return BadRequest(new { error = e.Message });
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        try
        {
            var response = await _logoutUseCase.ExecuteAsync(request);

            if (!response.Revoked)
            {
                _logger.LogInformation("Logout with refresh token already revoked or expired");
            }

            return NoContent();
        }
        catch (InvalidRefreshTokenException e)
        {
            _logger.LogWarning(e, "Logout rejected: {Reason}", e.Message);
            return Unauthorized(new { error = e.Message });
        }
    }
}
