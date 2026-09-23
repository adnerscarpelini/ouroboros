namespace Ouroboros.Auth.Api.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using Ouroboros.Auth.Api.Configuration;
using Ouroboros.Auth.Api.Models;
using Ouroboros.Auth.Application.UseCases.ConfirmEmail;
using Ouroboros.Auth.Application.UseCases.GetUser;
using Ouroboros.Auth.Application.UseCases.RegisterUser;
using Ouroboros.Auth.Application.UseCases.RequestPasswordReset;
using Ouroboros.Auth.Application.UseCases.ResetPassword;
using Ouroboros.Auth.Domain.Exceptions;
using Ouroboros.Auth.Infrastructure.Security;

[ApiController]
[Route("api/users")]
public sealed class UserController : ControllerBase
{
    private readonly IRegisterUserUseCase _registerUserUseCase;
    private readonly IConfirmEmailUseCase _confirmEmailUseCase;
    private readonly IRequestPasswordResetUseCase _requestPasswordResetUseCase;
    private readonly IResetPasswordUseCase _resetPasswordUseCase;
    private readonly IGetUserUseCase _getUserUseCase;
    private readonly ILogger<UserController> _logger;

    public UserController(
        IRegisterUserUseCase registerUserUseCase,
        IConfirmEmailUseCase confirmEmailUseCase,
        IRequestPasswordResetUseCase requestPasswordResetUseCase,
        IResetPasswordUseCase resetPasswordUseCase,
        IGetUserUseCase getUserUseCase,
        ILogger<UserController> logger)
    {
        _registerUserUseCase = registerUserUseCase;
        _confirmEmailUseCase = confirmEmailUseCase;
        _requestPasswordResetUseCase = requestPasswordResetUseCase;
        _resetPasswordUseCase = resetPasswordUseCase;
        _getUserUseCase = getUserUseCase;
        _logger = logger;
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitingConfiguration.UserRegisterPolicy)]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
    {
        try
        {
            var response = await _registerUserUseCase.ExecuteAsync(request);

            if (response.EmailConfirmationToken is not null)
            {
                // TODO: remover quando existir envio de e-mail via mensageria — logar token e temporario, so pra dev.
                _logger.LogInformation(
                    "Email confirmation token generated for user {UserId}: {EmailConfirmationToken}",
                    response.UserId,
                    response.EmailConfirmationToken);
            }
            else
            {
                _logger.LogWarning("User registration ignored: email already in use");
            }

            // Resposta identica com e-mail novo ou ja cadastrado, pra nao permitir enumeracao de e-mails.
            return Accepted(new { message = "If the email is available, a confirmation link will be sent to it." });
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
    [EnableRateLimiting(RateLimitingConfiguration.PasswordResetRequestPolicy)]
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

    [HttpPost("password-reset/confirm")]
    [EnableRateLimiting(RateLimitingConfiguration.PasswordResetConfirmPolicy)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            var response = await _resetPasswordUseCase.ExecuteAsync(request);

            _logger.LogInformation("Password reset completed for user {UserId}", response.UserId);

            return NoContent();
        }
        catch (DomainException e)
        {
            _logger.LogWarning(e, "Password reset rejected: {Reason}", e.Message);
            return BadRequest(new { error = e.Message });
        }
    }

    [HttpGet("{externalId:guid}")]
    [Authorize]
    public Task<IActionResult> GetById(Guid externalId)
    {
        return GetUserAsync(externalId, null, null);
    }

    [HttpPost("search")]
    [Authorize]
    public Task<IActionResult> Search([FromBody] SearchUserBody body)
    {
        return GetUserAsync(null, body.Login, body.Email);
    }

    private async Task<IActionResult> GetUserAsync(
        Guid? externalId,
        string? login,
        string? email)
    {
        var requesterId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(requesterId, out var requesterExternalId))
        {
            _logger.LogWarning("User lookup rejected: access token without a valid subject");
            return Unauthorized(new { error = "Invalid access token" });
        }

        var request = new GetUserRequest(
            requesterExternalId,
            User.FindFirstValue(JwtRegisteredClaimNames.UniqueName) ?? string.Empty,
            User.FindFirstValue(JwtRegisteredClaimNames.Email) ?? string.Empty,
            User.FindFirstValue(JwtTokenGenerator.RoleClaimType) ?? string.Empty,
            externalId,
            login,
            email);

        try
        {
            var response = await _getUserUseCase.ExecuteAsync(request);
            return Ok(response);
        }
        catch (AccessDeniedException e)
        {
            _logger.LogWarning(e, "User lookup denied for requester {RequesterId}: {Reason}", requesterExternalId, e.Message);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = e.Message });
        }
        catch (UserNotFoundException e)
        {
            _logger.LogWarning(e, "User lookup by requester {RequesterId} found nothing: {Reason}", requesterExternalId, e.Message);
            return NotFound(new { error = e.Message });
        }
        catch (DomainException e)
        {
            _logger.LogWarning(e, "User lookup rejected for requester {RequesterId}: {Reason}", requesterExternalId, e.Message);
            return BadRequest(new { error = e.Message });
        }
    }
}
