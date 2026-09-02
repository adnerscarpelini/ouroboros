using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Ouroboros.Modules.Auth.Application;
using Ouroboros.Modules.Auth.Domain;

namespace Ouroboros.Modules.Auth.Infrastructure;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
	private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(1);

	private readonly AuthOptions _authOptions;

	public JwtTokenGenerator(AuthOptions authOptions)
	{
		_authOptions = authOptions;
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

		var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_authOptions.JwtSigningKey));
		var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

		var token = new JwtSecurityToken(
			issuer: _authOptions.JwtIssuer,
			audience: _authOptions.JwtAudience,
			claims: claims,
			expires: expiresAt,
			signingCredentials: signingCredentials
		);

		var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

		return new AccessTokenResult(accessToken, expiresAt);
	}
}
