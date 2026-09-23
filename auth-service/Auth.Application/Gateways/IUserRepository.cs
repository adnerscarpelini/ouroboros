namespace Ouroboros.Auth.Application.Gateways;

using Ouroboros.Auth.Domain.Entities;

public interface IUserRepository
{
    Task AddAsync(User user);

    Task<bool> ExistsByLoginOrEmailAsync(string login, string email);

    Task<User?> GetByExternalIdAsync(Guid externalId);

    Task<User?> GetByEmailAsync(string email);

    Task<User?> GetByLoginAsync(string login);

    Task<User?> GetByLoginOrEmailAsync(string loginOrEmail);

    Task UpdateAsync(User user);
}
