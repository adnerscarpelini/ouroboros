namespace Ouroboros.Auth.Application.UseCases.ResetPassword;

public record ResetPasswordRequest(
    string Token,
    string NewPassword);
