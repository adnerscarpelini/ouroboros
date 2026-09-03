using Microsoft.EntityFrameworkCore;
using Ouroboros.Services.Auth.Application;
using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Infrastructure;

public sealed class UserRepository : IUserRepository
{
	private readonly AuthDbContext _dbContext;

	public UserRepository(AuthDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public void Add(User user)
	{
		_dbContext.Users.Add(user);
	}

	public Task<User?> GetByLoginAsync(
		string login,
		CancellationToken cancellationToken
	)
	{
		return _dbContext.Users.SingleOrDefaultAsync(u => u.Login == login, cancellationToken);
	}

	public Task<User?> GetByEmailAsync(
		string email,
		CancellationToken cancellationToken
	)
	{
		return _dbContext.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);
	}

	public Task<bool> ExistsByLoginAsync(
		string login,
		CancellationToken cancellationToken
	)
	{
		return _dbContext.Users.AnyAsync(u => u.Login == login, cancellationToken);
	}

	public Task<bool> ExistsByEmailAsync(
		string email,
		CancellationToken cancellationToken
	)
	{
		return _dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken);
	}
}
