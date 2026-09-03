using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Ouroboros.Services.Auth.Application;
using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Infrastructure;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
	private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(1);

	private readonly JwtOptions _jwtOptions;

	// Carregada uma vez e mantida viva pelo tempo de vida do serviço (registrado como Singleton).
	// O cache interno de SignatureProvider do Microsoft.IdentityModel.Tokens guarda referência à
	// chave usada na primeira assinatura; se essa chave for descartada (ex.: "using" por chamada),
	// a próxima chamada reusa o provider em cache apontando pra um objeto RSA já descartado —
	// ObjectDisposedException em RSABCrypt.GetKey(). Por isso a chave nunca é descartada aqui,
	// do mesmo jeito que a chave pública de validação em Program.cs.
	private readonly RsaSecurityKey _signingKey;

	public JwtTokenGenerator(JwtOptions jwtOptions)
	{
		_jwtOptions = jwtOptions;

		var rsa = RSA.Create();
		rsa.ImportFromPem(jwtOptions.SigningKeyPem);

		// O KeyId vai no cabeçalho do token como "kid" e é o mesmo publicado no JWKS: é assim que
		// um serviço validador sabe qual chave do conjunto usar, e é o que torna rotação possível.
		_signingKey = new RsaSecurityKey(rsa)
		{
			KeyId = JwtKeyId.ComputeFrom(rsa)
		};
	}

	public AccessTokenResult GenerateToken(User user)
	{
		var expiresAt = DateTime.UtcNow.Add(AccessTokenLifetime);

		var claims = new[]
		{
			new Claim(JwtRegisteredClaimNames.Sub, user.ExternalId.ToString()),
			new Claim(JwtRegisteredClaimNames.UniqueName, user.Login),
			new Claim(JwtRegisteredClaimNames.Email, user.Email)
		};

		var signingCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);

		var token = new JwtSecurityToken(
			issuer: _jwtOptions.Issuer,
			audience: _jwtOptions.Audience,
			claims: claims,
			expires: expiresAt,
			signingCredentials: signingCredentials
		);

		var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

		return new AccessTokenResult(accessToken, expiresAt);
	}
}
