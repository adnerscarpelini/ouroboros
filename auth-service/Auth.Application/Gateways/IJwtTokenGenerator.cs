namespace Ouroboros.Auth.Application.Gateways;

using Ouroboros.Auth.Domain.Entities;

public interface IJwtTokenGenerator
{
    /// <param name="sessionId">Vira o claim <c>sid</c>: identifica a sessao (familia de refresh tokens) do access token.</param>
    AccessToken Generate(
        Guid userId,
        string login,
        string email,
        UserRole role,
        Guid sessionId);
}
