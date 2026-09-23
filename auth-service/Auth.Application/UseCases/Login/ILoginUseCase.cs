namespace Ouroboros.Auth.Application.UseCases.Login;

public interface ILoginUseCase
{
    Task<LoginResponse> ExecuteAsync(LoginRequest request);
}
