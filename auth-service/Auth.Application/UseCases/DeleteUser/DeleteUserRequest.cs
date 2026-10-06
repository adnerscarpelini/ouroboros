namespace Ouroboros.Auth.Application.UseCases.DeleteUser;

/// <summary>
/// <c>RequesterId</c> vem do <c>sub</c> do access token validado, nunca do corpo da requisicao. O perfil do solicitante
/// NAO vem do token: o caso de uso o le do banco, pra um perfil alterado valer na hora.
/// <c>RequesterPassword</c> e a senha atual de quem esta excluindo (reautenticacao), nunca a da conta alvo.
/// </summary>
public record DeleteUserRequest(
    Guid RequesterId,
    string RequesterPassword,
    Guid ExternalId);
