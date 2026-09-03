using System.Text.Json.Serialization;

namespace Ouroboros.Services.Auth.Api.Contracts.WellKnown;

// Subconjunto mínimo do documento de descoberta OpenID Connect: só o necessário para que o
// JwtBearer de outro serviço encontre o JWKS sozinho a partir de uma Authority. O Auth não é um
// provedor OIDC completo — não há authorization_endpoint, token_endpoint nem fluxo de consentimento.
public sealed record OpenIdConfigurationResponse(
	[property: JsonPropertyName("issuer")] string Issuer,
	[property: JsonPropertyName("jwks_uri")] string JwksUri,
	[property: JsonPropertyName("id_token_signing_alg_values_supported")] IReadOnlyCollection<string> IdTokenSigningAlgValuesSupported,
	[property: JsonPropertyName("response_types_supported")] IReadOnlyCollection<string> ResponseTypesSupported,
	[property: JsonPropertyName("subject_types_supported")] IReadOnlyCollection<string> SubjectTypesSupported
);
