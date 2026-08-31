using System.ComponentModel.DataAnnotations;
using Ouroboros.Api.Validation;

namespace Ouroboros.Api.Contracts.Auth;

public sealed record ResetPasswordRequest
{
	[Required]
	public string Token { get; init; } = string.Empty;

	[Required]
	[StrongPassword]
	public string NewPassword { get; init; } = string.Empty;
}
