namespace Ouroboros.Auth.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Ouroboros.Auth.Application.UseCases.Login;
using Ouroboros.Auth.Domain.Exceptions;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ILoginUseCase _loginUseCase;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ILoginUseCase loginUseCase,
        ILogger<AuthController> logger)
    {
        _loginUseCase = loginUseCase;
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
}
