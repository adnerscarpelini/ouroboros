using System.ComponentModel.DataAnnotations;

namespace Ouroboros.Services.Auth.Api.Contracts.Auth;

public sealed record ConfirmEmailRequest
{
	[Required]
	public string Token { get; init; } = string.Empty;
}
