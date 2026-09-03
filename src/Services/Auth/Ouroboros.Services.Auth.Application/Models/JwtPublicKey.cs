namespace Ouroboros.Services.Auth.Application;

// Material público da chave que assina os tokens, no formato em que um JWKS o publica.
// Modulus e Exponent já vêm codificados em base64url, como manda a RFC 7517.
public sealed record JwtPublicKey(
	string KeyId,
	string Algorithm,
	string Modulus,
	string Exponent
);
