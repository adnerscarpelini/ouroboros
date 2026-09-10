using Ouroboros.Services.Notifications.Application;

namespace Ouroboros.Services.Notifications.Infrastructure;

public sealed class UnitOfWork : IUnitOfWork
{
	private readonly NotificationsDbContext _dbContext;

	public UnitOfWork(NotificationsDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public Task SaveChangesAsync(CancellationToken cancellationToken)
	{
		return _dbContext.SaveChangesAsync(cancellationToken);
	}
}
