namespace Ouroboros.Auth.Api.Models;

/// <summary>
/// Corpo de <c>DELETE /api/users/{externalId}</c>: senha atual de quem esta logado fazendo a exclusao (reautenticacao).
/// </summary>
public record DeleteUserBody(string Password);
