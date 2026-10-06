namespace Ouroboros.Auth.Api.Health;

using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Ouroboros.Auth.Infrastructure.Persistence;

/// <summary>
/// Readiness: confere o SQL Server com um <c>SELECT 1</c> de timeout de 2 s. O resultado nunca leva a mensagem da
/// excecao, a connection string nem o nome do host.
/// </summary>
public sealed class SqlServerHealthCheck : IHealthCheck
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    private readonly SqlConnectionFactory _connectionFactory;
    private readonly ILogger<SqlServerHealthCheck> _logger;

    public SqlServerHealthCheck(
        SqlConnectionFactory connectionFactory,
        ILogger<SqlServerHealthCheck> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            await using var connection = _connectionFactory.Create();
            await connection.OpenAsync(timeout.Token);
            await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1;", commandTimeout: (int)Timeout.TotalSeconds, cancellationToken: timeout.Token));

            return HealthCheckResult.Healthy();
        }
        catch (Exception e)
        {
            // O detalhe vai so pro log (so as falhas sao logadas). A resposta HTTP leva apenas o status.
            _logger.LogError(e, "SQL Server readiness check failed");
            return HealthCheckResult.Unhealthy();
        }
    }
}
