namespace Ouroboros.Auth.Application.UseCases.GetUser;

/// <summary>
/// Os dados do solicitante (<c>Requester*</c>) vem do access token validado, nunca do corpo da requisicao.
/// Exatamente um criterio de busca (<c>ExternalId</c>, <c>Login</c> ou <c>Email</c>) deve ser informado.
/// </summary>
public record GetUserRequest(
    Guid RequesterId,
    string RequesterLogin,
    string RequesterEmail,
    string RequesterRole,
    Guid? ExternalId,
    string? Login,
    string? Email);
