using System.ComponentModel.DataAnnotations;

namespace Ouroboros.AuthService.Api.Contracts.Auth;

public sealed record RefreshTokenRequest
{
	[Required]
	public string RefreshToken { get; init; } = string.Empty;
}
