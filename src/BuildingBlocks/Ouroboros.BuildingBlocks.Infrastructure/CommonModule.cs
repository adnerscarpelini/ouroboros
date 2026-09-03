using Microsoft.Extensions.DependencyInjection;
using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.BuildingBlocks.Infrastructure;

public static class CommonModule
{
	// TDbContext é o DbContext concreto do serviço chamador (ex.: AuthDbContext), exposto aqui também
	// como AppDbContext — o tipo base que ErrorLogService/EmailQueueService conhecem. É assim que cada
	// serviço persiste ErrorLog/EmailMessage na própria base sem que BuildingBlocks conheça serviço algum.
	// O parâmetro de tipo é obrigatório de propósito: antes esse registro era escrito à mão no
	// Add<NomeDoServico>Module de cada serviço e, se esquecido, o projeto compilava e só quebrava em
	// runtime dentro do GlobalExceptionHandler — mascarando o erro original. Agora é o compilador que cobra.
	// Ver docs/0000 - Arquitetura.md, seção "BuildingBlocks".
	public static IServiceCollection AddCommon<TDbContext>(this IServiceCollection services)
		where TDbContext : AppDbContext
	{
		services.AddScoped<AppDbContext>(serviceProvider => serviceProvider.GetRequiredService<TDbContext>());

		services.AddScoped<IErrorLogService, ErrorLogService>();
		services.AddScoped<IEmailQueueService, EmailQueueService>();

		return services;
	}
}
