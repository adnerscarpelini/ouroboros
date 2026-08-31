using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Modules.Auth.Application;

namespace Ouroboros.Modules.Auth.Infrastructure;

public static class AuthModule
{
	public static IServiceCollection AddAuthModule(
		this IServiceCollection services,
		string connectionString,
		string apiBaseUrl,
		string jwtSigningKey,
		string jwtIssuer,
		string jwtAudience
	)
	{
		services.AddDbContext<AuthDbContext>(options => options
			.UseNpgsql(connectionString)
			.UseSnakeCaseNamingConvention());

		services.AddSingleton(new AuthOptions(
			ApiBaseUrl: apiBaseUrl,
			JwtSigningKey: jwtSigningKey,
			JwtIssuer: jwtIssuer,
			JwtAudience: jwtAudience
		));

		services.AddScoped<IUserService, UserService>();
		services.AddScoped<IPasswordHasher, Argon2PasswordHasher>();
		services.AddScoped<ITokenGenerator, TokenGenerator>();
		services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

		return services;
	}
}
