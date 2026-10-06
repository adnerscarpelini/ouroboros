namespace Ouroboros.Auth.Application.Gateways;

using Ouroboros.Auth.Domain.Entities;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken refreshToken);

    Task<RefreshToken?> GetByHashAsync(string tokenHash);

    /// <summary>
    /// Persiste a revogacao so se o token ainda nao estiver revogado nem expirado no banco (a data da revogacao e a
    /// referencia do prazo). Retorna <c>false</c> quando outra requisicao revogou o mesmo token antes (uso concorrente).
    /// </summary>
    Task<bool> TryRevokeAsync(RefreshToken refreshToken);

    /// <summary>
    /// Revoga, num unico comando, todos os refresh tokens ainda ativos do usuario, exceto os da sessao informada
    /// (a atual de quem trocou a senha). <see cref="Guid.Empty"/> nao preserva sessao nenhuma.
    /// </summary>
    Task RevokeAllActiveByUserExceptSessionAsync(
        Guid userExternalId,
        Guid exceptSessionId,
        DateTimeOffset revokedAt);

    /// <summary>
    /// Revoga, num unico comando, todos os refresh tokens ainda ativos (nao revogados e nao expirados) da sessao.
    /// As outras sessoes do mesmo usuario nao sao afetadas.
    /// </summary>
    Task RevokeAllActiveBySessionAsync(Guid sessionId, DateTimeOffset revokedAt);

    /// <summary>
    /// Revoga, num unico comando, todos os refresh tokens ainda ativos (nao revogados e nao expirados) do usuario.
    /// </summary>
    Task RevokeAllActiveByUserAsync(Guid userExternalId, DateTimeOffset revokedAt);
}
