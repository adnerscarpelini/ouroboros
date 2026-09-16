using System.ComponentModel.DataAnnotations;

namespace Ouroboros.AuthService.Api.Contracts.Auth;

public sealed record ConfirmEmailRequest
{
	[Required]
	public string Token { get; init; } = string.Empty;
}
