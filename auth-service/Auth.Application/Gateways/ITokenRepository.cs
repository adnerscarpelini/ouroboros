namespace Ouroboros.Auth.Application.Gateways;

using Ouroboros.Auth.Domain.Entities;

public interface ITokenRepository
{
    Task AddAsync(Token token);

    Task<Token?> GetByHashAsync(string tokenHash, TokenType type);

    Task UpdateAsync(Token token);
}
