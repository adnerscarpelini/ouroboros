using Ouroboros.NotificationsService.Application;

namespace Ouroboros.NotificationsService.Application.Tests;

public sealed class FakeUnitOfWork : IUnitOfWork
{
	public int SaveChangesCount { get; private set; }

	public Task SaveChangesAsync(CancellationToken cancellationToken)
	{
		SaveChangesCount++;

		return Task.CompletedTask;
	}
}
