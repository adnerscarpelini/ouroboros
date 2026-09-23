namespace Ouroboros.Auth.Application.UseCases.Login;

public record LoginRequest(
    string Login,
    string Password);
