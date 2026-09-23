namespace Ouroboros.Auth.Application.Gateways;

using Ouroboros.Auth.Domain.Entities;

/// <remarks>
/// As buscas ignoram contas excluidas (exclusao logica): pra aplicacao, uma conta excluida nao existe.
/// </remarks>
public interface IUserRepository
{
    Task AddAsync(User user);

    Task<User?> GetByExternalIdAsync(Guid externalId);

    Task<User?> GetByEmailAsync(string email);

    Task<User?> GetByLoginAsync(string login);

    Task<User?> GetByLoginOrEmailAsync(string loginOrEmail);

    Task UpdateAsync(User user);

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
    /// Conta os Admins ativos e nao excluidos.
    /// </summary>
    Task<int> CountActiveAdminsAsync();
}
