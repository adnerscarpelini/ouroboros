namespace Ouroboros.Auth.Api.Configuration;

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

public static class RateLimitingConfiguration
{
    public const string PasswordResetRequestPolicy = "password-reset-request";
    public const string PasswordResetConfirmPolicy = "password-reset-confirm";
    public const string UserRegisterPolicy = "user-register";

    private static readonly TimeSpan PasswordResetWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan UserRegisterWindow = TimeSpan.FromMinutes(15);

    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Limite por IP: contem spam de e-mails pra mesma conta e varredura de logins a partir de uma origem.
            AddFixedWindowPerIpPolicy(
                options,
                PasswordResetRequestPolicy,
                5,
                PasswordResetWindow);

            // Mais folgado que a solicitacao: o usuario pode errar a politica de senha algumas vezes com o mesmo link.
            AddFixedWindowPerIpPolicy(
                options,
                PasswordResetConfirmPolicy,
                10,
                PasswordResetWindow);

            // Contem criacao de contas em massa e varredura de e-mails/logins a partir de uma origem.
            AddFixedWindowPerIpPolicy(
                options,
                UserRegisterPolicy,
                5,
                UserRegisterWindow);

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

    private static void AddFixedWindowPerIpPolicy(
        RateLimiterOptions options,
        string policyName,
        int permitLimit,
        TimeSpan window)
    {
        options.AddPolicy(policyName, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = 0,
                }));
    }
}
