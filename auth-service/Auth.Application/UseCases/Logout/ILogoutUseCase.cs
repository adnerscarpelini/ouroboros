namespace Ouroboros.Auth.Application.UseCases.Logout;

public interface ILogoutUseCase
{
    Task<LogoutResponse> ExecuteAsync(LogoutRequest request);
}
