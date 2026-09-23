namespace Ouroboros.Auth.Application.UseCases.RegisterUser;

/// <remarks>
/// TODO: remover <c>EmailConfirmationToken</c> quando existir envio de e-mail via mensageria —
/// hoje o token so e devolvido aqui pra permitir confirmar o cadastro em dev (Swagger/Postman).
/// </remarks>
public record RegisterUserResponse(
    Guid Id,
    string Login,
    string FullName,
    string Email,
    string Role,
    string EmailConfirmationToken);
