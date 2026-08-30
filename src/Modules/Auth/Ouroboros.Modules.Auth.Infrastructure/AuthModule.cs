using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Modules.Auth.Application;

namespace Ouroboros.Modules.Auth.Infrastructure;

public static class AuthModule
{
	public static IServiceCollection AddAuthModule(
		this IServiceCollection services,
		string connectionString
	)
	{
		services.AddDbContext<AuthDbContext>(options => options
			.UseNpgsql(connectionString)
			.UseSnakeCaseNamingConvention());

		services.AddScoped<IUserService, UserService>();

		return services;
	}
}
