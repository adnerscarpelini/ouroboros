namespace Ouroboros.Auth.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Ouroboros.Auth.Application.UseCases.ConfirmEmail;
using Ouroboros.Auth.Application.UseCases.RegisterUser;
using Ouroboros.Auth.Domain.Exceptions;

[ApiController]
[Route("api/users")]
public sealed class UserController : ControllerBase
{
    private readonly IRegisterUserUseCase _registerUserUseCase;
    private readonly IConfirmEmailUseCase _confirmEmailUseCase;
    private readonly ILogger<UserController> _logger;

    public UserController(
        IRegisterUserUseCase registerUserUseCase,
        IConfirmEmailUseCase confirmEmailUseCase,
        ILogger<UserController> logger)
    {
        _registerUserUseCase = registerUserUseCase;
        _confirmEmailUseCase = confirmEmailUseCase;
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
}
