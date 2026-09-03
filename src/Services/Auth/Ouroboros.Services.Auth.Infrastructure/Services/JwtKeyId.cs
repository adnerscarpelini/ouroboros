using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Ouroboros.Services.Auth.Infrastructure;

internal static class JwtKeyId
{
	// Thumbprint RFC 7638 da chave. Depende só do módulo e do expoente — que existem tanto na chave
	// privada quanto na pública —, então quem assina e quem publica o JWKS chegam ao mesmo "kid"
	// sem precisar combinar um valor à mão. Trocar o par de chaves troca o kid automaticamente.
	public static string ComputeFrom(RSA rsa)
	{
		var jsonWebKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa));

		return Base64UrlEncoder.Encode(jsonWebKey.ComputeJwkThumbprint());
	}
}
