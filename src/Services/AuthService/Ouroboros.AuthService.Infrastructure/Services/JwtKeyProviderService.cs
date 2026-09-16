using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Ouroboros.AuthService.Application;

namespace Ouroboros.AuthService.Infrastructure;

public sealed class JwtKeyProviderService : IJwtKeyProvider
{
	private readonly JwtPublicKey _publicKey;

	public JwtKeyProviderService(JwtOptions jwtOptions)
	{
		using var rsa = RSA.Create();
		rsa.ImportFromPem(jwtOptions.PublicKeyPem);

		var jsonWebKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa));

		_publicKey = new JwtPublicKey(
			KeyId: JwtKeyId.ComputeFrom(rsa),
			Algorithm: SecurityAlgorithms.RsaSha256,
			Modulus: jsonWebKey.N,
			Exponent: jsonWebKey.E
		);
	}

	public JwtPublicKey GetPublicKey()
	{
		return _publicKey;
	}
}
