using System.ComponentModel.DataAnnotations;

namespace Ouroboros.Api.Contracts.Auth;

public sealed record RegisterUserRequest
{
	[Required]
	public string Login { get; init; } = string.Empty;

	[Required]
	public string FullName { get; init; } = string.Empty;

	[Required]
	[EmailAddress]
	public string Email { get; init; } = string.Empty;

	[Required]
	public string Password { get; init; } = string.Empty;
}
