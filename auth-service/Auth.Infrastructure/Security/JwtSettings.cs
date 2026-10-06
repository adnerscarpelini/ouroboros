namespace Ouroboros.Auth.Infrastructure.Security;

using System.ComponentModel.DataAnnotations;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    // URL publica base do auth-service (em dev, http://localhost:8082). E o "issuer" dos tokens e o que o documento de
    // descoberta (OpenID Connect Discovery) devolve: quem configura so a Authority valida o token por ele.
    [Required]
    public string Issuer { get; init; } = null!;

    [Required]
    public string Audience { get; init; } = null!;

    [Range(1, 1440)]
    public int AccessTokenExpirationMinutes { get; init; }

    [Range(1, 365)]
    public int RefreshTokenExpirationDays { get; init; }

    // Chaves RSA de assinatura (RS256). Exatamente uma Active (assina) e as demais Published (so entram no JWKS, pra os
    // tokens antigos continuarem validos durante a rotacao). Chave aposentada nao tem status: so sai da lista.
    public List<SigningKeySettings> SigningKeys { get; init; } = new();
}

public enum SigningKeyStatus
{
    Active,
    Published,
}

public sealed class SigningKeySettings
{
    // Identificador da chave, no header "kid" de todo JWT e no JWKS.
    public string Kid { get; init; } = string.Empty;

    // Caminho de um arquivo PEM com a chave privada RSA (em dev, fora do git; em producao, um Docker secret montado).
    public string PrivateKeyPath { get; init; } = string.Empty;

    public SigningKeyStatus Status { get; init; }
}
