namespace Ouroboros.Auth.Api.Configuration;

using Microsoft.Extensions.Options;
using Ouroboros.Auth.Api.Services;

public sealed class TokenCleanupSettings
{
    public const string SectionName = "TokenCleanup";

    public bool Enabled { get; set; }

    /// <summary>Intervalo entre os ciclos de limpeza.</summary>
    public TimeSpan Interval { get; set; }

    /// <summary>Quanto tempo uma linha fica depois de expirar antes de ser apagada.</summary>
    public TimeSpan Retention { get; set; }
}

// Validada no startup (ValidateOnStart): intervalo e retencao precisam ser positivos.
public sealed class TokenCleanupSettingsValidator : IValidateOptions<TokenCleanupSettings>
{
    public ValidateOptionsResult Validate(
        string? name,
        TokenCleanupSettings options)
    {
        var errors = new List<string>();

        if (options.Interval <= TimeSpan.Zero)
        {
            errors.Add("TokenCleanup:Interval must be greater than zero.");
        }

        if (options.Retention <= TimeSpan.Zero)
        {
            errors.Add("TokenCleanup:Retention must be greater than zero.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

public static class TokenCleanupConfiguration
{
    public static IServiceCollection AddTokenCleanup(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<TokenCleanupSettings>()
            .Bind(configuration.GetSection(TokenCleanupSettings.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<TokenCleanupSettings>, TokenCleanupSettingsValidator>();
        services.AddHostedService<ExpiredTokenCleanupService>();

        return services;
    }
}
