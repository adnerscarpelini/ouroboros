using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Ouroboros.Services.Auth.Application;

namespace Ouroboros.Services.Auth.Infrastructure;

public sealed class JwtKeyProvider : IJwtKeyProvider
{
	private readonly JwtPublicKey _publicKey;

	public JwtKeyProvider(JwtOptions jwtOptions)
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
