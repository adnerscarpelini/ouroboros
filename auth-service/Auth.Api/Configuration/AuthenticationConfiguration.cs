namespace Ouroboros.Auth.Api.Configuration;

using System.Text;
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

        // Configurado a partir do JwtSettings ja validado no startup, o mesmo usado pra emitir o token.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((options, jwtSettings) =>
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
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                    // Aceita so o algoritmo usado na emissao, contra ataques de troca de algoritmo (ex.: "none").
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = JwtTokenGenerator.RoleClaimType,
                };
            });

        services.AddAuthorization();

        return services;
    }
}
