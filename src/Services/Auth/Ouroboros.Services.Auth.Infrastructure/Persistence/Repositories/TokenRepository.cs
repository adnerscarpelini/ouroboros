using Microsoft.EntityFrameworkCore;
using Ouroboros.Services.Auth.Application;
using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Infrastructure;

public sealed class TokenRepository : ITokenRepository
{
	private readonly AuthDbContext _dbContext;

	public TokenRepository(AuthDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public void Add(Token token)
	{
		_dbContext.Tokens.Add(token);
	}

	public Task<Token?> GetByHashAsync(
		string tokenHash,
		CancellationToken cancellationToken
	)
	{
		return _dbContext.Tokens
			.Include(t => t.TokenType)
			.Include(t => t.User)
			.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
	}

	public async Task<IReadOnlyCollection<Token>> GetPendingByUserAndTypeAsync(
		User user,
		string tokenTypeName,
		CancellationToken cancellationToken
	)
	{
		return await _dbContext.Tokens
			.Where(t => t.UserId == user.Id && t.TokenType.Name == tokenTypeName && !t.Validated)
			.ToListAsync(cancellationToken);
	}
}
