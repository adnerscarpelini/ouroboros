using Ouroboros.BuildingBlocks.Infrastructure;
using Ouroboros.NotificationsService.Application;

namespace Ouroboros.NotificationsService.Infrastructure;

public sealed class UnitOfWork : IUnitOfWork
{
	private readonly DbSession _session;

	public UnitOfWork(DbSession session)
	{
		_session = session;
	}

	public async Task SaveChangesAsync(CancellationToken cancellationToken)
	{
		if (_session.Transaction is null)
		{
			await _session.BeginTransactionAsync(cancellationToken);
		}

		try
		{
			await _session.FlushAsync(cancellationToken);
			await _session.CommitAsync(cancellationToken);
		}
		catch
		{
			await _session.RollbackAsync(cancellationToken);
			throw;
		}
	}
}
