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
    /// Persiste o uso do token so se ele ainda nao estiver usado e nao tiver expirado no banco (em <c>token.UsedAt</c>).
    /// Retorna <c>false</c> quando outra requisicao usou o mesmo token antes (uso concorrente) ou quando ele expirou ou foi invalidado.
    /// </summary>
    Task<bool> TryMarkAsUsedAsync(Token token);

    /// <summary>
    /// Apaga, num so comando (uma transacao curta), ate <paramref name="batchSize"/> tokens com
    /// <c>expires_at</c> anterior a <paramref name="expiredBefore"/>. Devolve quantos apagou: menos que o lote
    /// significa que nao sobrou nenhum. Token ainda nao expirado nunca e apagado, mesmo usado.
    /// </summary>
    Task<int> DeleteExpiredBatchAsync(
        DateTimeOffset expiredBefore,
        int batchSize);

    /// <summary>
    /// Invalida, num unico comando, todos os tokens pendentes (nao usados e nao expirados) do usuario
    /// com o tipo informado, antecipando a expiracao deles para <paramref name="invalidatedAt"/>.
    /// </summary>
    Task InvalidatePendingByUserAsync(
        Guid userExternalId,
        TokenType type,
        DateTimeOffset invalidatedAt);
}
