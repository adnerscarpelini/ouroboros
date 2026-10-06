namespace Ouroboros.Auth.Api.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using Ouroboros.Auth.Api.Configuration;
using Ouroboros.Auth.Application.UseCases.Login;
using Ouroboros.Auth.Application.UseCases.Logout;
using Ouroboros.Auth.Application.UseCases.LogoutAll;
using Ouroboros.Auth.Application.UseCases.RefreshAccessToken;
using Ouroboros.Auth.Domain.Exceptions;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ILoginUseCase _loginUseCase;
    private readonly IRefreshAccessTokenUseCase _refreshAccessTokenUseCase;
    private readonly ILogoutUseCase _logoutUseCase;
    private readonly ILogoutAllUseCase _logoutAllUseCase;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ILoginUseCase loginUseCase,
        IRefreshAccessTokenUseCase refreshAccessTokenUseCase,
        ILogoutUseCase logoutUseCase,
        ILogoutAllUseCase logoutAllUseCase,
        ILogger<AuthController> logger)
    {
        _loginUseCase = loginUseCase;
        _refreshAccessTokenUseCase = refreshAccessTokenUseCase;
        _logoutUseCase = logoutUseCase;
        _logoutAllUseCase = logoutAllUseCase;
        _logger = logger;
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitingConfiguration.AuthLoginPolicy)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var response = await _loginUseCase.ExecuteAsync(request);
            return Ok(response);
        }
        catch (InvalidCredentialsException e)
        {
            _logger.LogWarning(e, "Login rejected for {Login} from {RemoteIp}: {Reason}", request.Login, HttpContext.Connection.RemoteIpAddress, e.Message);
            return Unauthorized(new { error = e.Message });
        }
        catch (DomainException e)
        {
            _logger.LogWarning(e, "Login rejected for {Login} from {RemoteIp}: {Reason}", request.Login, HttpContext.Connection.RemoteIpAddress, e.Message);
            return BadRequest(new { error = e.Message });
        }
    }

    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitingConfiguration.AuthRefreshPolicy)]
    public async Task<IActionResult> Refresh([FromBody] RefreshAccessTokenRequest request)
    {
        try
        {
            var response = await _refreshAccessTokenUseCase.ExecuteAsync(request);
            return Ok(response);
        }
        catch (RefreshTokenReuseException e)
        {
            // So o externalId do usuario e o id da sessao: nunca o token.
            _logger.LogWarning(
                "Refresh token reuse detected for user {UserId} in session {SessionId}: the session was revoked",
                e.UserExternalId,
                e.SessionId);
            return Unauthorized(new { error = e.Message });
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

    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAll()
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(subject, out var userId))
        {
            _logger.LogWarning("Logout of all sessions rejected: access token without a valid subject");
            return Unauthorized(new { error = "Invalid access token" });
        }

        await _logoutAllUseCase.ExecuteAsync(new LogoutAllRequest(userId));

        _logger.LogInformation("All sessions ended for user {UserId}", userId);

        return NoContent();
    }
}
