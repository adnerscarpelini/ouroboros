using Microsoft.EntityFrameworkCore;
using Ouroboros.Services.Auth.Application;
using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Infrastructure;

public sealed class TokenTypeRepository : ITokenTypeRepository
{
	private readonly AuthDbContext _dbContext;

	public TokenTypeRepository(AuthDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public Task<TokenType> GetByNameAsync(
		string name,
		CancellationToken cancellationToken
	)
	{
		return _dbContext.TokenTypes.SingleAsync(t => t.Name == name, cancellationToken);
	}
}
