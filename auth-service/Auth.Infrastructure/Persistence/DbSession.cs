namespace Ouroboros.Auth.Infrastructure.Persistence;

using Dapper;
using Microsoft.Data.SqlClient;

/// <summary>
/// Um por request. Fora de uma unidade de trabalho, cada comando abre a propria conexao e roda em autocommit.
/// Dentro dela, todos os comandos usam a mesma conexao e a mesma transacao.
/// </summary>
public sealed class DbSession : IAsyncDisposable
{
    private readonly SqlConnectionFactory _connectionFactory;

    private SqlConnection? _connection;
    private SqlTransaction? _transaction;

    public DbSession(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public bool InTransaction => _transaction is not null;

    public Task<int> ExecuteAsync(
        string sql,
        object? parameters = null)
    {
        return RunAsync((connection, transaction) => connection.ExecuteAsync(sql, parameters, transaction));
    }

    public Task<T?> QuerySingleOrDefaultAsync<T>(
        string sql,
        object? parameters = null)
    {
        return RunAsync((connection, transaction) => connection.QuerySingleOrDefaultAsync<T>(sql, parameters, transaction));
    }

    public Task<T?> ExecuteScalarAsync<T>(
        string sql,
        object? parameters = null)
    {
        return RunAsync((connection, transaction) => connection.ExecuteScalarAsync<T>(sql, parameters, transaction));
    }

    public async Task BeginAsync()
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("A unit of work is already in progress; nested units of work are not supported.");
        }

        _connection = _connectionFactory.Create();
        await _connection.OpenAsync();
        _transaction = (SqlTransaction)await _connection.BeginTransactionAsync();
    }

    public async Task CommitAsync()
    {
        await _transaction!.CommitAsync();
    }

    // Sem commit previo, descartar a transacao faz o rollback.
    public async Task EndAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    public ValueTask DisposeAsync() => new(EndAsync());

    private async Task<T> RunAsync<T>(Func<SqlConnection, SqlTransaction?, Task<T>> command)
    {
        if (_connection is not null)
        {
            return await command(_connection, _transaction);
        }

        await using var connection = _connectionFactory.Create();

        return await command(connection, null);
    }
}
