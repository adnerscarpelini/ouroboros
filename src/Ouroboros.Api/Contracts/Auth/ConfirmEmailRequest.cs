using System.ComponentModel.DataAnnotations;

namespace Ouroboros.Api.Contracts.Auth;

public sealed record ConfirmEmailRequest
{
	[Required]
	public string Token { get; init; } = string.Empty;
}
