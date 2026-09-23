namespace Ouroboros.Auth.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Ouroboros.Auth.Api.Configuration;
using Ouroboros.Auth.Application.UseCases.ConfirmEmail;
using Ouroboros.Auth.Application.UseCases.RegisterUser;
using Ouroboros.Auth.Application.UseCases.RequestPasswordReset;
using Ouroboros.Auth.Domain.Exceptions;

[ApiController]
[Route("api/users")]
public sealed class UserController : ControllerBase
{
    private readonly IRegisterUserUseCase _registerUserUseCase;
    private readonly IConfirmEmailUseCase _confirmEmailUseCase;
    private readonly IRequestPasswordResetUseCase _requestPasswordResetUseCase;
    private readonly ILogger<UserController> _logger;

    public UserController(
        IRegisterUserUseCase registerUserUseCase,
        IConfirmEmailUseCase confirmEmailUseCase,
        IRequestPasswordResetUseCase requestPasswordResetUseCase,
        ILogger<UserController> logger)
    {
        _registerUserUseCase = registerUserUseCase;
        _confirmEmailUseCase = confirmEmailUseCase;
        _requestPasswordResetUseCase = requestPasswordResetUseCase;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
    {
        try
        {
            var response = await _registerUserUseCase.ExecuteAsync(request);

            // TODO: remover quando existir envio de e-mail via mensageria — logar token e temporario, so pra dev.
            _logger.LogInformation(
                "Email confirmation token generated for user {UserId}: {EmailConfirmationToken}",
                response.Id,
                response.EmailConfirmationToken);

            return Created($"/api/users/{response.Id}", response);
        }
        catch (DomainException e)
        {
            _logger.LogWarning(e, "User registration rejected: {Reason}", e.Message);
            return BadRequest(new { error = e.Message });
        }
    }

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request)
    {
        try
        {
            var response = await _confirmEmailUseCase.ExecuteAsync(request);
            return Ok(response);
        }
        catch (DomainException e)
        {
            _logger.LogWarning(e, "Email confirmation rejected: {Reason}", e.Message);
            return BadRequest(new { error = e.Message });
        }
    }

    [HttpPost("password-reset/request")]
    [EnableRateLimiting(RateLimitingConfiguration.PasswordResetPolicy)]
    public async Task<IActionResult> RequestPasswordReset([FromBody] RequestPasswordResetRequest request)
    {
        try
        {
            var response = await _requestPasswordResetUseCase.ExecuteAsync(request);

            if (response.PasswordResetToken is not null)
            {
                // TODO: remover quando existir envio de e-mail via mensageria — logar token e temporario, so pra dev.
                _logger.LogInformation(
                    "Password reset token generated for user {UserId}: {PasswordResetToken}",
                    response.UserId,
                    response.PasswordResetToken);
            }

            // Resposta identica exista ou nao a conta, pra nao permitir enumeracao de usuarios.
            return Accepted(new { message = "If the account exists, a password reset link will be sent to its email." });
        }
        catch (DomainException e)
        {
            _logger.LogWarning(e, "Password reset request rejected: {Reason}", e.Message);
            return BadRequest(new { error = e.Message });
        }
    }
}
