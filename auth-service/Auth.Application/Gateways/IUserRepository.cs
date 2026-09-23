namespace Ouroboros.Auth.Application.Gateways;

using Ouroboros.Auth.Domain.Entities;

public interface IUserRepository
{
    Task AddAsync(User user);

    Task<bool> ExistsByLoginOrEmailAsync(string login, string email);

    Task<User?> GetByExternalIdAsync(Guid externalId);

    Task<User?> GetByLoginAsync(string login);

    Task UpdateAsync(User user);
}
