using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Modules.Auth.Application;

namespace Ouroboros.Modules.Auth.Infrastructure;

public static class AuthModule
{
	public static IServiceCollection AddAuthModule(
		this IServiceCollection services,
		string connectionString,
		string apiBaseUrl
	)
	{
		services.AddDbContext<AuthDbContext>(options => options
			.UseNpgsql(connectionString)
			.UseSnakeCaseNamingConvention());

		services.AddSingleton(new AuthOptions(apiBaseUrl));

		services.AddScoped<IUserService, UserService>();
		services.AddScoped<IPasswordHasher, Argon2PasswordHasher>();
		services.AddScoped<ITokenGenerator, TokenGenerator>();

		return services;
	}
}
