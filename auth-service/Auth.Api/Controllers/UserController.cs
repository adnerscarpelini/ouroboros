namespace Ouroboros.Auth.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Ouroboros.Auth.Application.UseCases.RegisterUser;
using Ouroboros.Auth.Domain.Exceptions;

[ApiController]
[Route("api/users")]
public sealed class UserController : ControllerBase
{
    private readonly IRegisterUserUseCase _registerUserUseCase;
    private readonly ILogger<UserController> _logger;

    public UserController(
        IRegisterUserUseCase registerUserUseCase,
        ILogger<UserController> logger)
    {
        _registerUserUseCase = registerUserUseCase;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
    {
        try
        {
            var response = await _registerUserUseCase.ExecuteAsync(request);
            return Created($"/api/users/{response.Id}", response);
        }
        catch (DomainException e)
        {
            _logger.LogWarning(e, "User registration rejected: {Reason}", e.Message);
            return BadRequest(new { error = e.Message });
        }
    }
}
