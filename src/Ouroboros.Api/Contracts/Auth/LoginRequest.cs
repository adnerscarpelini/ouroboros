using System.ComponentModel.DataAnnotations;

namespace Ouroboros.Api.Contracts.Auth;

public sealed record LoginRequest
{
	[Required]
	public string Login { get; init; } = string.Empty;

	[Required]
	public string Password { get; init; } = string.Empty;
}
