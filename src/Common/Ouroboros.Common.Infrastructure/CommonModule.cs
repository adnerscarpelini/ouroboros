using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Common.Application;

namespace Ouroboros.Common.Infrastructure;

public static class CommonModule
{
	public static IServiceCollection AddCommon(
		this IServiceCollection services,
		string connectionString
	)
	{
		services.AddDbContext<CommonDbContext>(options => options
			.UseNpgsql(connectionString)
			.UseSnakeCaseNamingConvention());

		services.AddScoped<IErrorLogService, ErrorLogService>();

		return services;
	}
}
