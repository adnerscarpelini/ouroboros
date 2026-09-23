namespace Ouroboros.Auth.Api.Models;

/// <summary>
/// Corpo de <c>POST /api/users/search</c>. Login/e-mail vao no corpo, nunca na URL, pra nao vazar em logs de proxy/gateway.
/// </summary>
public record SearchUserBody(
    string? Login,
    string? Email);
