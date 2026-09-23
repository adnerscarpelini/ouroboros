namespace Ouroboros.Auth.Application.UseCases.DeleteUser;

/// <summary>
/// Os dados do solicitante (<c>Requester*</c>) vem do access token validado, nunca do corpo da requisicao.
/// <c>RequesterPassword</c> e a senha atual de quem esta excluindo (reautenticacao), nunca a da conta alvo.
/// </summary>
public record DeleteUserRequest(
    Guid RequesterId,
    string RequesterRole,
    string RequesterPassword,
    Guid ExternalId);
