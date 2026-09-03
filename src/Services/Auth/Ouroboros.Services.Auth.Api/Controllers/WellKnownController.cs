using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ouroboros.Services.Auth.Api.Contracts.WellKnown;
using Ouroboros.Services.Auth.Application;

namespace Ouroboros.Services.Auth.Api.Controllers;

// Endpoints públicos por definição: quem precisa deles é um serviço que ainda não consegue validar
// nenhum token — exigir autenticação aqui seria circular.
[AllowAnonymous]
[ApiController]
[Route(".well-known")]
public sealed class WellKnownController : ControllerBase
{
	private readonly IJwtKeyProvider _jwtKeyProvider;
	private readonly AuthApplicationOptions _options;
	private readonly IConfiguration _configuration;

	public WellKnownController(
		IJwtKeyProvider jwtKeyProvider,
		AuthApplicationOptions options,
		IConfiguration configuration
	)
	{
		_jwtKeyProvider = jwtKeyProvider;
		_options = options;
		_configuration = configuration;
	}

	[HttpGet("jwks.json")]
	public ActionResult<JwksResponse> GetJwks()
	{
		var publicKey = _jwtKeyProvider.GetPublicKey();

		return Ok(new JwksResponse(
		[
			new JwksKeyResponse(
				KeyType: "RSA",
				Use: "sig",
				Algorithm: publicKey.Algorithm,
				KeyId: publicKey.KeyId,
				Modulus: publicKey.Modulus,
				Exponent: publicKey.Exponent
			)
		]));
	}

	[HttpGet("openid-configuration")]
	public ActionResult<OpenIdConfigurationResponse> GetOpenIdConfiguration()
	{
		var issuer = _configuration["Jwt:Issuer"]!;

		return Ok(new OpenIdConfigurationResponse(
			Issuer: issuer,
			JwksUri: $"{_options.PublicBaseUrl}/.well-known/jwks.json",
			IdTokenSigningAlgValuesSupported: ["RS256"],
			ResponseTypesSupported: ["token"],
			SubjectTypesSupported: ["public"]
		));
	}
}
