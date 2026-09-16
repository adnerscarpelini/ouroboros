using Ouroboros.Services.Auth.Application;
using Ouroboros.BuildingBlocks.Infrastructure;

namespace Ouroboros.Services.Auth.Infrastructure;

public sealed class UnitOfWork : IUnitOfWork
{
	private readonly DbSession _session;

	public UnitOfWork(DbSession session)
	{
		_session = session;
	}

	public async Task SaveChangesAsync(CancellationToken cancellationToken)
	{
		var ownsTransaction = _session.Transaction is null;

		if (ownsTransaction)
		{
			await _session.BeginTransactionAsync(cancellationToken);
		}

		try
		{
			await _session.FlushAsync(cancellationToken);

			if (ownsTransaction)
			{
				await _session.CommitAsync(cancellationToken);
			}
		}
		catch
		{
			if (ownsTransaction)
			{
				await _session.RollbackAsync(cancellationToken);
			}

			throw;
		}
	}

	public Task ExecuteInTransactionAsync(
		Func<CancellationToken, Task> operation,
		CancellationToken cancellationToken)
	{
		return ExecuteInTransactionAsync(
			async transactionCancellationToken =>
			{
				await operation(transactionCancellationToken);
				return true;
			},
			cancellationToken);
	}

	public async Task<TResult> ExecuteInTransactionAsync<TResult>(
		Func<CancellationToken, Task<TResult>> operation,
		CancellationToken cancellationToken)
	{
		if (_session.Transaction is not null)
		{
			throw new InvalidOperationException(
				"Não é permitido iniciar uma transação SQL dentro de outra transação SQL.");
		}

		await _session.BeginTransactionAsync(cancellationToken);

		try
		{
			var result = await operation(cancellationToken);
			await _session.FlushAsync(cancellationToken);
			await _session.CommitAsync(cancellationToken);

			return result;
		}
		catch
		{
			await _session.RollbackAsync(cancellationToken);
			throw;
		}
	}
}
