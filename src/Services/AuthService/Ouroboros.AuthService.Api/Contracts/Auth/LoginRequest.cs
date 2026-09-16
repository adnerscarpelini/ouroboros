using System.ComponentModel.DataAnnotations;

namespace Ouroboros.AuthService.Api.Contracts.Auth;

public sealed record LoginRequest
{
	[Required]
	public string Login { get; init; } = string.Empty;

	[Required]
	public string Password { get; init; } = string.Empty;
}
