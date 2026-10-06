namespace Ouroboros.Auth.Api.Configuration;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Ouroboros.Auth.Infrastructure.Security;

public static class AuthenticationConfiguration
{
    // Tolerancia de relogio curta: o padrao do ASP.NET (5 min) estenderia demais a vida de um access token de poucos minutos.
    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Configurado a partir do JwtSettings ja validado no startup, o mesmo usado pra emitir o token. O auth-service
        // valida os proprios tokens com o conjunto local de chaves publicas, sem chamada HTTP a si mesmo.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>, SigningKeyStore>((options, jwtSettings, signingKeys) =>
            {
                var settings = jwtSettings.Value;

                // Mantem os nomes curtos dos claims (sub, role...) em vez de mapear pros URIs do ClaimTypes.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = ClockSkew,
                    ValidateIssuerSigningKey = true,

                    // Chaves Active e Published: durante a rotacao, tokens da chave antiga continuam validos.
                    IssuerSigningKeys = signingKeys.PublicKeys,

                    // Aceita so RS256, contra ataques de troca de algoritmo (ex.: "none", ou HS256 com a chave publica).
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = JwtTokenGenerator.RoleClaimType,
                };
            });

        services.AddAuthorization();

        return services;
    }
}
