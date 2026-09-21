namespace Ouroboros.Auth.Application.UseCases.RegisterUser;

public record RegisterUserResponse(
    Guid Id,
    string Login,
    string FullName,
    string Email);
