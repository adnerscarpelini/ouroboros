namespace Ouroboros.Auth.Application.UseCases.GetUser;

/// <summary>
/// <c>RequesterId</c> vem do <c>sub</c> do access token validado, nunca do corpo da requisicao. Perfil, login e e-mail
/// do solicitante NAO vem do token: o caso de uso os le do banco, pra um perfil alterado valer na hora.
/// Exatamente um criterio de busca (<c>ExternalId</c>, <c>Login</c> ou <c>Email</c>) deve ser informado.
/// </summary>
public record GetUserRequest(
    Guid RequesterId,
    Guid? ExternalId,
    string? Login,
    string? Email);
