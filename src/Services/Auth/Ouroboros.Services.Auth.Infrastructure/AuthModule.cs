using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.BuildingBlocks.Infrastructure;
using Ouroboros.Services.Auth.Application;

namespace Ouroboros.Services.Auth.Infrastructure;

public static class AuthModule
{
	public static IServiceCollection AddAuthModule(
		this IServiceCollection services,
		string connectionString,
		string apiBaseUrl,
		string jwtSigningKeyPem,
		string jwtIssuer,
		string jwtAudience
	)
	{
		services.AddDbContext<AuthDbContext>(options => options
			.UseNpgsql(connectionString)
			.UseSnakeCaseNamingConvention());

		// Expõe o AuthDbContext como AppDbContext: é o que permite ErrorLogService/EmailQueueService
		// (BuildingBlocks, que só conhecem o tipo base) persistirem na própria base do Auth.
		services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<AuthDbContext>());

		services.AddSingleton(new AuthOptions(
			ApiBaseUrl: apiBaseUrl,
			JwtSigningKeyPem: jwtSigningKeyPem,
			JwtIssuer: jwtIssuer,
			JwtAudience: jwtAudience
		));

		services.AddScoped<IUserService, UserService>();
		services.AddScoped<IPasswordHasher, Argon2PasswordHasher>();
		services.AddScoped<ITokenGenerator, TokenGenerator>();
		// Singleton: carrega a chave RSA uma única vez e não a descarta — ver comentário em JwtTokenGenerator.
		services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

		return services;
	}
}
