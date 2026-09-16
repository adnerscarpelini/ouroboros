using System.Data.Common;
using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.BuildingBlocks.Infrastructure;

public sealed class DbSession : IAsyncDisposable
{
	private readonly IDbConnectionFactory _connectionFactory;
	private readonly List<
		Func<DbConnection, DbTransaction, CancellationToken, Task>> _pendingCommands = [];
	private DbConnection? _connection;

	public DbConnection Connection =>
		_connection ?? throw new InvalidOperationException("A sessão ainda não foi aberta.");

	public DbTransaction? Transaction { get; private set; }

	public DbSession(IDbConnectionFactory connectionFactory)
	{
		_connectionFactory = connectionFactory;
	}

	public async Task OpenAsync(CancellationToken cancellationToken)
	{
		if (_connection is not null)
		{
			return;
		}

		_connection = await _connectionFactory.OpenAsync(cancellationToken);
	}

	public async Task BeginTransactionAsync(CancellationToken cancellationToken)
	{
		if (Transaction is not null)
		{
			throw new InvalidOperationException("A sessão já possui uma transação aberta.");
		}

		if (_connection is null)
		{
			await OpenAsync(cancellationToken);
		}

		Transaction = await Connection.BeginTransactionAsync(cancellationToken);
	}

	public void Enqueue(
		Func<DbConnection, DbTransaction, CancellationToken, Task> command)
	{
		ArgumentNullException.ThrowIfNull(command);
		_pendingCommands.Add(command);
	}

	public async Task FlushAsync(CancellationToken cancellationToken)
	{
		if (Transaction is null)
		{
			throw new InvalidOperationException(
				"Não é possível executar comandos pendentes sem uma transação aberta.");
		}

		try
		{
			foreach (var command in _pendingCommands)
			{
				await command(Connection, Transaction, cancellationToken);
			}
		}
		finally
		{
			// Mesmo que um comando falhe no meio da fila, nenhum deles pode sobreviver para a
			// próxima transação: a transação atual será revertida pelo chamador, então repetir
			// esses comandos depois executaria de novo (ou pela primeira vez) algo que o
			// chamador já trata como desfeito.
			_pendingCommands.Clear();
		}
	}

	public async Task CommitAsync(CancellationToken cancellationToken)
	{
		if (Transaction is null)
		{
			throw new InvalidOperationException("A sessão não possui uma transação aberta.");
		}

		await Transaction.CommitAsync(cancellationToken);
		await Transaction.DisposeAsync();
		Transaction = null;
	}

	public async Task RollbackAsync(CancellationToken cancellationToken)
	{
		// Um comando enfileirado antes da falha (ex.: repository.Add chamado, mas nunca
		// enviado ao banco porque a operação lançou antes do FlushAsync) não pode sobreviver
		// à transação revertida — senão ele seria executado na próxima transação desta sessão.
		_pendingCommands.Clear();

		if (Transaction is null)
		{
			return;
		}

		await Transaction.RollbackAsync(cancellationToken);
		await Transaction.DisposeAsync();
		Transaction = null;
	}

	public async ValueTask DisposeAsync()
	{
		_pendingCommands.Clear();

		if (Transaction is not null)
		{
			await Transaction.DisposeAsync();
		}

		if (_connection is not null)
		{
			await _connection.DisposeAsync();
		}
	}
}
