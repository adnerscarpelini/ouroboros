namespace Ouroboros.Services.Auth.Application.Tests;

public sealed class FakeUnitOfWork : IUnitOfWork
{
	public int SaveChangesCount { get; private set; }
	public int TransactionCount { get; private set; }

	public Task SaveChangesAsync(CancellationToken cancellationToken)
	{
		SaveChangesCount++;

		return Task.CompletedTask;
	}

	public async Task ExecuteInTransactionAsync(
		Func<CancellationToken, Task> operation,
		CancellationToken cancellationToken
	)
	{
		TransactionCount++;

		await operation(cancellationToken);
	}

	public async Task<TResult> ExecuteInTransactionAsync<TResult>(
		Func<CancellationToken, Task<TResult>> operation,
		CancellationToken cancellationToken
	)
	{
		TransactionCount++;

		return await operation(cancellationToken);
	}
}
