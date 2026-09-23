namespace Ouroboros.Auth.Application.UseCases.ConfirmEmail;

public interface IConfirmEmailUseCase
{
    Task<ConfirmEmailResponse> ExecuteAsync(ConfirmEmailRequest request);
}
