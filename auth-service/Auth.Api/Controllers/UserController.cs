namespace Ouroboros.Auth.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Ouroboros.Auth.Application.UseCases.RegisterUser;
using Ouroboros.Auth.Domain.Exceptions;

[ApiController]
[Route("api/users")]
public sealed class UserController : ControllerBase
{
    private readonly IRegisterUserUseCase _registerUserUseCase;

    public UserController(IRegisterUserUseCase registerUserUseCase)
    {
        _registerUserUseCase = registerUserUseCase;
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
            return BadRequest(new { error = e.Message });
        }
    }
}
