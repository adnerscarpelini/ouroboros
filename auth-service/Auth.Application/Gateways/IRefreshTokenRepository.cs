namespace Ouroboros.Auth.Application.Gateways;

using Ouroboros.Auth.Domain.Entities;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken refreshToken);
}
