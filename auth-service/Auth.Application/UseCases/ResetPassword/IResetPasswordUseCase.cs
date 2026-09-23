namespace Ouroboros.Auth.Application.UseCases.ResetPassword;

public interface IResetPasswordUseCase
{
    Task<ResetPasswordResponse> ExecuteAsync(ResetPasswordRequest request);
}
