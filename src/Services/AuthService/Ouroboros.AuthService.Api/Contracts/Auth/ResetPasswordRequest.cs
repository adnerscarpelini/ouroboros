using System.ComponentModel.DataAnnotations;
using Ouroboros.AuthService.Api.Validation;

namespace Ouroboros.AuthService.Api.Contracts.Auth;

public sealed record ResetPasswordRequest
{
	[Required]
	public string Token { get; init; } = string.Empty;

	[Required]
	[StrongPassword]
	public string NewPassword { get; init; } = string.Empty;
}
