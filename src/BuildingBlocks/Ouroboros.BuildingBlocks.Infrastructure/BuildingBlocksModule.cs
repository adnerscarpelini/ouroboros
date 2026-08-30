using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.BuildingBlocks.Infrastructure;

public static class BuildingBlocksModule
{
	public static IServiceCollection AddBuildingBlocks(
		this IServiceCollection services,
		string connectionString
	)
	{
		services.AddDbContext<BuildingBlocksDbContext>(options => options
			.UseNpgsql(connectionString)
			.UseSnakeCaseNamingConvention());

		services.AddScoped<IErrorLogService, ErrorLogService>();

		return services;
	}
}
