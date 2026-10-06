namespace Ouroboros.Auth.Api.Configuration;

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

public static class RateLimitingConfiguration
{
    public const string PasswordResetRequestPolicy = "password-reset-request";
    public const string PasswordResetConfirmPolicy = "password-reset-confirm";
    public const string UserRegisterPolicy = "user-register";
    public const string UserDeletePolicy = "user-delete";
    public const string PasswordChangePolicy = "password-change";
    public const string AuthLoginPolicy = "auth-login";
    public const string AuthRefreshPolicy = "auth-refresh";
    public const string EmailConfirmPolicy = "email-confirm";

    private static readonly string[] Policies =
    [
        PasswordResetRequestPolicy,
        PasswordResetConfirmPolicy,
        UserRegisterPolicy,
        UserDeletePolicy,
        PasswordChangePolicy,
        AuthLoginPolicy,
        AuthRefreshPolicy,
        EmailConfirmPolicy,
    ];

    public static IServiceCollection AddRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var policies = Policies.Select(policy =>
        {
            var section = configuration.GetSection($"RateLimiting:{policy}");
            var permitLimit = section.GetValue<int?>("PermitLimit");
            var window = section.GetValue<TimeSpan?>("Window");

            if (permitLimit is null or <= 0 || !window.HasValue || window.Value <= TimeSpan.Zero)
            {
                throw new InvalidOperationException($"Invalid rate limit settings for {policy}.");
            }

            return (Name: policy, PermitLimit: permitLimit.Value, Window: window.Value);
        }).ToArray();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            foreach (var policy in policies)
            {
                AddFixedWindowPerIpPolicy(
                    options,
                    policy.Name,
                    policy.PermitLimit,
                    policy.Window);
            }

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
