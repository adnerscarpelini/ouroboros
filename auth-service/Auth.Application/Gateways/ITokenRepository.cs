namespace Ouroboros.Auth.Application.Gateways;

using Ouroboros.Auth.Domain.Entities;

public interface ITokenRepository
{
    Task AddAsync(Token token);

    Task<Token?> GetByHashAsync(string tokenHash, TokenType type);

    /// <summary>
    /// Indica se o usuario tem algum token do tipo informado ainda pendente (nao usado e nao expirado em <paramref name="now"/>).
    /// </summary>
    Task<bool> ExistsPendingByUserAsync(
        Guid userExternalId,
        TokenType type,
        DateTimeOffset now);

    Task UpdateAsync(Token token);

    /// <summary>
    /// Persiste o uso do token so se ele ainda nao estiver usado no banco.
    /// Retorna <c>false</c> quando outra requisicao usou o mesmo token antes (uso concorrente).
    /// </summary>
    Task<bool> TryMarkAsUsedAsync(Token token);

    /// <summary>
    /// Invalida, num unico comando, todos os tokens pendentes (nao usados e nao expirados) do usuario
    /// com o tipo informado, antecipando a expiracao deles para <paramref name="invalidatedAt"/>.
    /// </summary>
    Task InvalidatePendingByUserAsync(
        Guid userExternalId,
        TokenType type,
        DateTimeOffset invalidatedAt);
}
