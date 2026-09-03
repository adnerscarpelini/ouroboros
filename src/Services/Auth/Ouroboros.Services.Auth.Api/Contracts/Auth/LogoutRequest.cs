using System.ComponentModel.DataAnnotations;

namespace Ouroboros.Services.Auth.Api.Contracts.Auth;

public sealed record LogoutRequest
{
	[Required]
	public string RefreshToken { get; init; } = string.Empty;
}
