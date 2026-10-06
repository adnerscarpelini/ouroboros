namespace Ouroboros.Auth.Application.UseCases.LogoutAll;

/// <summary>
/// <c>UserId</c> vem do <c>sub</c> do access token validado, nunca do corpo da requisicao.
/// </summary>
public record LogoutAllRequest(Guid UserId);
