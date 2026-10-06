namespace Ouroboros.Auth.Application.UseCases.ChangePassword;

/// <summary>
/// <c>UserId</c> e <c>SessionId</c> vem do <c>sub</c> e do <c>sid</c> do access token validado, nunca do corpo da
/// requisicao. Sem <c>sid</c> (token anterior a spec 2026092506) nao ha sessao atual a preservar: todas sao encerradas.
/// </summary>
public record ChangePasswordRequest(
    Guid UserId,
    Guid? SessionId,
    string CurrentPassword,
    string NewPassword);
