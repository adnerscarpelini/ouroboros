using Microsoft.EntityFrameworkCore;
using Ouroboros.Services.Auth.Application;
using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Infrastructure;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
	private readonly AuthDbContext _dbContext;

	public RefreshTokenRepository(AuthDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public void Add(RefreshToken refreshToken)
	{
		_dbContext.RefreshTokens.Add(refreshToken);
	}

	public Task<RefreshToken?> GetByHashAsync(
		string tokenHash,
		CancellationToken cancellationToken
	)
	{
		return _dbContext.RefreshTokens
			.Include(t => t.User)
			.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
	}
}
