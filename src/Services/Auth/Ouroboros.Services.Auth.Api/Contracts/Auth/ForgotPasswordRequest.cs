using System.ComponentModel.DataAnnotations;

namespace Ouroboros.Services.Auth.Api.Contracts.Auth;

public sealed record ForgotPasswordRequest
{
	[Required]
	[EmailAddress]
	public string Email { get; init; } = string.Empty;
}
