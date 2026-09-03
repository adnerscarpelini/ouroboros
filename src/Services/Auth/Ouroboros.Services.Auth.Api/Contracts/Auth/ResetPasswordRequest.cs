using System.ComponentModel.DataAnnotations;
using Ouroboros.Services.Auth.Api.Validation;

namespace Ouroboros.Services.Auth.Api.Contracts.Auth;

public sealed record ResetPasswordRequest
{
	[Required]
	public string Token { get; init; } = string.Empty;

	[Required]
	[StrongPassword]
	public string NewPassword { get; init; } = string.Empty;
}
