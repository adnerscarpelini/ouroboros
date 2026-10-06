namespace Ouroboros.Auth.Api.Health;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog.Events;

public static class HealthConfiguration
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";

    private const string ReadyTag = "ready";

    public static IServiceCollection AddServiceHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>("sqlserver", tags: [ReadyTag]);

        return services;
    }

    // Anonimos, sem [EnableRateLimiting] (so as politicas nomeadas limitam) e com a resposta so de status
    // ("Healthy" ou "Unhealthy"): sem mensagem de excecao, connection string nem nome de host.
    public static IEndpointRouteBuilder MapServiceHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        // Liveness: nao depende de nada, so confirma que o processo responde.
        endpoints.MapHealthChecks(LivePath, new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous();

        // Readiness: confere o SQL Server. Unhealthy responde 503.
        endpoints.MapHealthChecks(ReadyPath, new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) })
            .AllowAnonymous();

        return endpoints;
    }

    // As sondas batem a cada poucos segundos: health bem-sucedido nao entra no log de requisicoes em Information. So as
    // falhas (5xx ou excecao) sao logadas, como qualquer outra requisicao.
    public static LogEventLevel GetRequestLogLevel(
        HttpContext context,
        double elapsedMilliseconds,
        Exception? exception)
    {
        var failed = exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError;

        if (failed)
        {
            return LogEventLevel.Error;
        }

        return IsHealthRequest(context) ? LogEventLevel.Verbose : LogEventLevel.Information;
    }

    private static bool IsHealthRequest(HttpContext context)
    {
        return context.Request.Path.Equals(LivePath, StringComparison.OrdinalIgnoreCase)
            || context.Request.Path.Equals(ReadyPath, StringComparison.OrdinalIgnoreCase);
    }
}
