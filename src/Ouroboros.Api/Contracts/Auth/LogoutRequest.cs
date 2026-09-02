using System.ComponentModel.DataAnnotations;

namespace Ouroboros.Api.Contracts.Auth;

public sealed record LogoutRequest
{
	[Required]
	public string RefreshToken { get; init; } = string.Empty;
}
