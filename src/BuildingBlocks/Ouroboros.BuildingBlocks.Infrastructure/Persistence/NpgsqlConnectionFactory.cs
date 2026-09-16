using System.Data.Common;
using Npgsql;
using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.BuildingBlocks.Infrastructure;

public sealed class NpgsqlConnectionFactory : IDbConnectionFactory, IAsyncDisposable
{
	private readonly NpgsqlDataSource _dataSource;

	public NpgsqlConnectionFactory(string connectionString)
	{
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			throw new ArgumentException("A connection string não pode ser vazia.", nameof(connectionString));
		}

		_dataSource = NpgsqlDataSource.Create(connectionString);
	}

	public Task<DbConnection> OpenAsync(CancellationToken cancellationToken)
	{
		return OpenConnectionAsync(cancellationToken);
	}

	private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
	{
		return await _dataSource.OpenConnectionAsync(cancellationToken);
	}

	public ValueTask DisposeAsync()
	{
		return _dataSource.DisposeAsync();
	}
}
