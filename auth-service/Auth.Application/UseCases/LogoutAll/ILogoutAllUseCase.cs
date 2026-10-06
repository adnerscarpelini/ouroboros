namespace Ouroboros.Auth.Application.UseCases.LogoutAll;

public interface ILogoutAllUseCase
{
    Task<LogoutAllResponse> ExecuteAsync(LogoutAllRequest request);
}
