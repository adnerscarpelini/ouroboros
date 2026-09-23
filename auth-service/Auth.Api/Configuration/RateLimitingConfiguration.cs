namespace Ouroboros.Auth.Api.Configuration;

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

public static class RateLimitingConfiguration
{
    public const string PasswordResetPolicy = "password-reset";

    private const int PasswordResetPermitLimit = 5;
    private static readonly TimeSpan PasswordResetWindow = TimeSpan.FromMinutes(15);

    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Limite por IP: contem spam de e-mails pra mesma conta e varredura de logins a partir de uma origem.
            options.AddPolicy(PasswordResetPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = PasswordResetPermitLimit,
                        Window = PasswordResetWindow,
                        QueueLimit = 0,
                    }));

            options.OnRejected = (context, _) =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger(nameof(RateLimitingConfiguration));

                logger.LogWarning(
                    "Rate limit exceeded for {Path} from {RemoteIp}",
                    context.HttpContext.Request.Path,
                    context.HttpContext.Connection.RemoteIpAddress);

                return ValueTask.CompletedTask;
            };
        });

        return services;
    }
}
