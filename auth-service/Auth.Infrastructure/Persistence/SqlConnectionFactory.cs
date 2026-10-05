namespace Ouroboros.Auth.Infrastructure.Persistence;

using Microsoft.Data.SqlClient;

public sealed class SqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SqlConnection Create() => new(_connectionString);
}
