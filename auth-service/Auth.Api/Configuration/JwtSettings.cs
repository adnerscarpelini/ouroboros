namespace Ouroboros.Auth.Api.Configuration;

using System.ComponentModel.DataAnnotations;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    // HMAC-SHA256 exige chave de pelo menos 256 bits (32 bytes).
    // Nunca versionada: chega por variavel de ambiente (Jwt__SigningKey).
    [Required]
    [MinLength(32)]
    public string SigningKey { get; init; } = null!;

    [Required]
    public string Issuer { get; init; } = null!;

    [Required]
    public string Audience { get; init; } = null!;

    [Range(1, 1440)]
    public int AccessTokenExpirationMinutes { get; init; }

    [Range(1, 365)]
    public int RefreshTokenExpirationDays { get; init; }
}
