using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Ouroboros.BuildingBlocks.Application;
namespace Ouroboros.BuildingBlocks.Infrastructure;

public sealed class SqlHealthCheck : IHealthCheck
{
	private readonly IDbConnectionFactory _factory;
	public SqlHealthCheck(IDbConnectionFactory factory)=>_factory=factory;
	public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,CancellationToken cancellationToken=default){try{await using var c=await _factory.OpenAsync(cancellationToken);return HealthCheckResult.Healthy();}catch(Exception ex){return HealthCheckResult.Unhealthy("PostgreSQL indisponível.",ex);}}
}
