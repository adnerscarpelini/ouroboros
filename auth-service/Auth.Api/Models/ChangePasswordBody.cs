namespace Ouroboros.Auth.Api.Models;

public record ChangePasswordBody(
    string CurrentPassword,
    string NewPassword);
