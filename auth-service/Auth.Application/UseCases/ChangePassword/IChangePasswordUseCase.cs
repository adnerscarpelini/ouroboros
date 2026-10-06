namespace Ouroboros.Auth.Application.UseCases.ChangePassword;

public interface IChangePasswordUseCase
{
    Task<ChangePasswordResponse> ExecuteAsync(ChangePasswordRequest request);
}
