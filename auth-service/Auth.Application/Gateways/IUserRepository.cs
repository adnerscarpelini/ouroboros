namespace Ouroboros.Auth.Application.Gateways;

using Ouroboros.Auth.Domain.Entities;

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
    /// Usado so pra descartar cadastro abandonado (nunca confirmado).
    /// </summary>
    Task RemoveAsync(Guid externalId);
}
