using System.Security.Cryptography;
using System.Text;
using Ouroboros.Services.Auth.Application;

namespace Ouroboros.Services.Auth.Infrastructure;

public sealed class TokenGenerator : ITokenGenerator
{
	private const int TokenSizeInBytes = 32;

	public string GenerateToken()
	{
		var bytes = RandomNumberGenerator.GetBytes(TokenSizeInBytes);

		return Convert.ToBase64String(bytes)
			.Replace('+', '-')
			.Replace('/', '_')
			.TrimEnd('=');
	}

	public string Hash(string token)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));

		return Convert.ToHexString(bytes);
	}
}
