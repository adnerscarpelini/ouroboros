using System.Text.Json.Serialization;

namespace Ouroboros.Services.Auth.Api.Contracts.WellKnown;

// Formato definido pela RFC 7517 (JSON Web Key Set). Os nomes dos campos são fixos pelo padrão,
// por isso o JsonPropertyName explícito em vez da convenção de nomes do projeto.
public sealed record JwksResponse(
	[property: JsonPropertyName("keys")] IReadOnlyCollection<JwksKeyResponse> Keys
);

public sealed record JwksKeyResponse(
	[property: JsonPropertyName("kty")] string KeyType,
	[property: JsonPropertyName("use")] string Use,
	[property: JsonPropertyName("alg")] string Algorithm,
	[property: JsonPropertyName("kid")] string KeyId,
	[property: JsonPropertyName("n")] string Modulus,
	[property: JsonPropertyName("e")] string Exponent
);
