using System.ComponentModel.DataAnnotations;
using Ouroboros.Services.Auth.Api.Validation;

namespace Ouroboros.Services.Auth.Api.Contracts.Auth;

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
	[StrongPassword]
	public string Password { get; init; } = string.Empty;
}
