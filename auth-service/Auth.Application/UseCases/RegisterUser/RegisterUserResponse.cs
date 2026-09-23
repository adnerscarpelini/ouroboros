namespace Ouroboros.Auth.Application.UseCases.RegisterUser;

/// <summary>
/// Resultado interno do cadastro. Nunca vai no corpo da resposta HTTP: o cadastro responde sempre
/// com uma mensagem generica, pra nao revelar se o e-mail ja tem conta.
/// </summary>
/// <param name="UserId">Preenchido so quando o usuario foi criado.</param>
/// <param name="EmailConfirmationToken">
/// Token em claro, preenchido so quando o usuario foi criado.
/// TODO: remover quando existir envio de e-mail via mensageria — hoje o token so volta aqui pra ser logado em dev.
/// </param>
public record RegisterUserResponse(
    Guid? UserId,
    string? EmailConfirmationToken);
