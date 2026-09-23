namespace Ouroboros.Auth.Application.UseCases.ConfirmEmail;

public record ConfirmEmailResponse(
    Guid UserId,
    string Login,
    string Email);
