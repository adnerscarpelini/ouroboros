namespace Ouroboros.Auth.Application.Settings;

// A application nao conhece IOptions nem JwtSettings: a Api monta este record a partir da configuracao.
public record RefreshTokenSettings(TimeSpan Lifetime);
