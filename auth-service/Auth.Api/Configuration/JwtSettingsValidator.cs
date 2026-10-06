namespace Ouroboros.Auth.Api.Configuration;

using Microsoft.Extensions.Options;
using Ouroboros.Auth.Infrastructure.Security;

/// <summary>
/// Validacao do JWT no startup (alem das DataAnnotations do <see cref="JwtSettings"/>): o issuer precisa ser uma URL
/// publica absoluta e as chaves de assinatura precisam estar certas (ver <see cref="SigningKeyStore.Validate"/>).
/// </summary>
public sealed class JwtSettingsValidator : IValidateOptions<JwtSettings>
{
    public ValidateOptionsResult Validate(
        string? name,
        JwtSettings options)
    {
        var errors = new List<string>();

        if (!string.IsNullOrWhiteSpace(options.Issuer)
            && !(Uri.TryCreate(options.Issuer, UriKind.Absolute, out var issuer)
                && (issuer.Scheme == Uri.UriSchemeHttp || issuer.Scheme == Uri.UriSchemeHttps)))
        {
            errors.Add("Jwt:Issuer must be the public base URL of the auth-service (an absolute http or https URL).");
        }

        errors.AddRange(SigningKeyStore.Validate(options.SigningKeys));

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
