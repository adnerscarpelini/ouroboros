namespace Ouroboros.Auth.Application.UseCases.RequestPasswordReset;

/// <param name="UserId">Preenchido so quando um token foi gerado.</param>
/// <param name="PasswordResetToken">
/// Token em claro, preenchido so quando foi gerado. Nunca deve ir no corpo da resposta HTTP.
/// TODO: remover quando existir envio de e-mail via mensageria — hoje o token so volta aqui pra ser logado em dev.
/// </param>
public record RequestPasswordResetResponse(
    Guid? UserId,
    string? PasswordResetToken);
