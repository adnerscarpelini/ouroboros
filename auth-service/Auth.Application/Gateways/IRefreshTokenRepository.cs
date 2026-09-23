namespace Ouroboros.Auth.Application.Gateways;

using Ouroboros.Auth.Domain.Entities;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken refreshToken);

    Task<RefreshToken?> GetByHashAsync(string tokenHash);

    /// <summary>
    /// Persiste a revogacao so se o token ainda nao estiver revogado no banco.
    /// Retorna <c>false</c> quando outra requisicao revogou o mesmo token antes (uso concorrente).
    /// </summary>
    Task<bool> TryRevokeAsync(RefreshToken refreshToken);

    /// <summary>
    /// Revoga, num unico comando, todos os refresh tokens ainda ativos (nao revogados e nao expirados) do usuario.
    /// </summary>
    Task RevokeAllActiveByUserAsync(Guid userExternalId, DateTimeOffset revokedAt);
}
