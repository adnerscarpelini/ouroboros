namespace Ouroboros.Auth.Application.UseCases.RequestPasswordReset;

public interface IRequestPasswordResetUseCase
{
    Task<RequestPasswordResetResponse> ExecuteAsync(RequestPasswordResetRequest request);
}
