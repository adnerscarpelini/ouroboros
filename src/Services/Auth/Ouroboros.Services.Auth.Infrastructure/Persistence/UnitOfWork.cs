using Microsoft.EntityFrameworkCore;
using Ouroboros.Services.Auth.Application;

namespace Ouroboros.Services.Auth.Infrastructure;

public sealed class UnitOfWork : IUnitOfWork
{
	private readonly AuthDbContext _dbContext;

	public UnitOfWork(AuthDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public Task SaveChangesAsync(CancellationToken cancellationToken)
	{
		return _dbContext.SaveChangesAsync(cancellationToken);
	}

	public async Task ExecuteInTransactionAsync(
		Func<CancellationToken, Task> operation,
		CancellationToken cancellationToken
	)
	{
		await ExecuteInTransactionAsync(
			operation: async transactionCancellationToken =>
			{
				await operation(transactionCancellationToken);

				return true;
			},
			cancellationToken: cancellationToken
		);
	}

	public Task<TResult> ExecuteInTransactionAsync<TResult>(
		Func<CancellationToken, Task<TResult>> operation,
		CancellationToken cancellationToken
	)
	{
		// A operação roda dentro da estratégia de execução do provider, não o contrário: é ela que sabe
		// repetir tudo em caso de falha transitória, e um retry precisa refazer a transação inteira.
		var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

		return executionStrategy.ExecuteAsync(async () =>
		{
			await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

			var result = await operation(cancellationToken);

			await transaction.CommitAsync(cancellationToken);

			return result;
		});
	}
}
