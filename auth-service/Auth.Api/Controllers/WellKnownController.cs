namespace Ouroboros.Auth.Api.Controllers;

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Ouroboros.Auth.Infrastructure.Security;

/// <summary>
/// Dados publicos pra quem valida os tokens do auth-service. Anonimos e cacheaveis. O auth-service NAO e um provedor
/// OpenID Connect completo (nao tem authorization endpoint nem id_token): o documento de descoberta e o minimo pro
/// JwtBearer dos consumidores funcionar so com a Authority.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route(".well-known")]
public sealed class WellKnownController : ControllerBase
{
    private const string CacheControl = "public, max-age=3600";

    private readonly SigningKeyStore _signingKeys;
    private readonly JwtSettings _settings;

    public WellKnownController(
        SigningKeyStore signingKeys,
        IOptions<JwtSettings> settings)
    {
        _signingKeys = signingKeys;
        _settings = settings.Value;
    }

    // As chaves Active e Published, so com parametros publicos (RFC 7517).
    [HttpGet("jwks.json")]
    public IActionResult GetJwks()
    {
        Response.Headers.CacheControl = CacheControl;

        return Ok(new JwksResponse(_signingKeys.JsonWebKeys.Select(key => new JwkResponse(key.Kty, key.Use, key.Kid, key.Alg, key.N, key.E)).ToArray()));
    }

    [HttpGet("openid-configuration")]
    public IActionResult GetOpenIdConfiguration()
    {
        Response.Headers.CacheControl = CacheControl;

        var issuer = _settings.Issuer;

        return Ok(new OpenIdConfigurationResponse(issuer, $"{issuer.TrimEnd('/')}/.well-known/jwks.json"));
    }

    private sealed record JwksResponse(
        [property: JsonPropertyName("keys")] JwkResponse[] Keys);

    private sealed record JwkResponse(
        [property: JsonPropertyName("kty")] string Kty,
        [property: JsonPropertyName("use")] string Use,
        [property: JsonPropertyName("kid")] string Kid,
        [property: JsonPropertyName("alg")] string Alg,
        [property: JsonPropertyName("n")] string N,
        [property: JsonPropertyName("e")] string E);

    private sealed record OpenIdConfigurationResponse(
        [property: JsonPropertyName("issuer")] string Issuer,
        [property: JsonPropertyName("jwks_uri")] string JwksUri);
}
