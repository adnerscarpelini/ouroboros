namespace Ouroboros.Auth.Application.Gateways;

using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;

/// <remarks>
/// As buscas ignoram contas excluidas (exclusao logica): pra aplicacao, uma conta excluida nao existe.
/// Toda busca por login ou e-mail recebe o valor ja normalizado (<c>IdentityPolicy.Normalize</c>).
/// </remarks>
public interface IUserRepository
{
    /// <exception cref="DuplicateLoginException">O login normalizado ja pertence a outra conta.</exception>
    /// <exception cref="DuplicateEmailException">O e-mail normalizado ja pertence a outra conta ativa.</exception>
    Task AddAsync(User user);

    Task<User?> GetByExternalIdAsync(Guid externalId);

    Task<User?> GetByEmailAsync(string email);

    Task<User?> GetByLoginAsync(string login);

    Task<User?> GetByLoginOrEmailAsync(string loginOrEmail);

    Task UpdateAsync(User user);

    Task<bool> RecordFailedAccessAsync(
        Guid externalId,
        DateTimeOffset now);

    Task<bool> TryResetFailedAccessAsync(
        Guid externalId,
        DateTimeOffset now);

    /// <summary>
    /// Login bem-sucedido: grava <c>last_login_at</c> e zera a contagem de falhas, num so UPDATE, e so se a conta nao
    /// estiver bloqueada. Devolve <c>false</c> se estiver (bloqueio imposto entre a leitura e a escrita) ou excluida.
    /// </summary>
    Task<bool> TryRegisterLoginAsync(
        Guid externalId,
        DateTimeOffset now);

    /// <summary>
    /// Zera a contagem de falhas e o bloqueio da conta, mesmo que ela esteja bloqueada.
    /// Diferente de <see cref="TryResetFailedAccessAsync"/>, que so age fora do bloqueio, serve a quem
    /// acabou de provar que controla o e-mail da conta (redefinicao de senha).
    /// </summary>
    Task ClearLockoutAsync(
        Guid externalId,
        DateTimeOffset now);

    /// <summary>
    /// Troca o hash so se ele ainda for o lido pelo chamador (re-hash no login). Se a senha foi trocada em paralelo,
    /// nao afeta nada e devolve <c>false</c>, que e o correto: o hash novo vence.
    /// </summary>
    Task<bool> TryRehashPasswordAsync(
        Guid externalId,
        string currentPasswordHash,
        string newPasswordHash);

    /// <summary>
    /// Remove o usuario e, em cascata, os tokens e refresh tokens dele.
    /// Usado so pra descartar cadastro abandonado (nunca confirmado). Nunca remove conta excluida.
    /// </summary>
    Task RemoveAsync(Guid externalId);

    /// <summary>
    /// Indica se o login pertence a uma conta excluida. Login de conta excluida nunca e reaproveitado.
    /// </summary>
    Task<bool> ExistsDeletedByLoginAsync(string login);

    /// <summary>
    /// Conta os Admins ativos e nao excluidos <b>depois de tomar um lock exclusivo</b> que dura ate o fim da transacao
    /// corrente. Duas operacoes que reduzem o numero de Admins (excluir um deles) ficam em fila, e a segunda ja enxerga
    /// a contagem reduzida. Exige uma <see cref="IUnitOfWork"/> em andamento: fora dela lanca
    /// <see cref="InvalidOperationException"/>, porque o lock soltaria na hora e nao protegeria nada.
    /// </summary>
    Task<int> CountActiveAdminsForUpdateAsync();
}
