using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Services.Auth.Application;

namespace Ouroboros.Services.Auth.Infrastructure;

public static class AuthModule
{
	public static IServiceCollection AddAuthModule(
		this IServiceCollection services,
		string connectionString,
		string publicBaseUrl,
		string jwtSigningKeyPem,
		string jwtPublicKeyPem,
		string jwtIssuer,
		string jwtAudience
	)
	{
		services.AddDbContext<AuthDbContext>(options => options
			.UseNpgsql(connectionString)
			.UseSnakeCaseNamingConvention());

		services.AddSingleton(new AuthApplicationOptions(PublicBaseUrl: publicBaseUrl));

		services.AddSingleton(new JwtOptions(
			SigningKeyPem: jwtSigningKeyPem,
			PublicKeyPem: jwtPublicKeyPem,
			Issuer: jwtIssuer,
			Audience: jwtAudience
		));

		// Persistência: os casos de uso na Application só conhecem estas interfaces, nunca o DbContext.
		services.AddScoped<IUnitOfWork, UnitOfWork>();
		services.AddScoped<IUserRepository, UserRepository>();
		services.AddScoped<ITokenRepository, TokenRepository>();
		services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
		services.AddScoped<ITokenTypeRepository, TokenTypeRepository>();

		// Casos de uso (moram na Application).
		services.AddScoped<IUserRegistrationService, UserRegistrationService>();
		services.AddScoped<IAuthenticationService, AuthenticationService>();
		services.AddScoped<IPasswordResetService, PasswordResetService>();

		services.AddScoped<IPasswordHasher, Argon2PasswordHasher>();
		services.AddScoped<ITokenGenerator, TokenGenerator>();
		services.AddScoped<IEmailTemplateRenderer, EmailTemplateRenderer>();
		// Singleton: carrega a chave RSA uma única vez e não a descarta — ver comentário em JwtTokenGenerator.
		services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
		// Singleton: o material público da chave é calculado uma vez e não muda em tempo de execução.
		services.AddSingleton<IJwtKeyProvider, JwtKeyProvider>();

		return services;
	}
}
