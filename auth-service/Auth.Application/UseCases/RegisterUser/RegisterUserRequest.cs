namespace Ouroboros.Auth.Application.UseCases.RegisterUser;

public record RegisterUserRequest(
    string Login,
    string FullName,
    string Email,
    string Password);
