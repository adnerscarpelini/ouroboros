using Microsoft.Extensions.DependencyInjection;
using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.BuildingBlocks.Infrastructure;

public static class CommonModule
{
	// Não registra nenhum DbContext próprio: ErrorLogService/EmailQueueService dependem do AppDbContext
	// já registrado pelo serviço chamador (ver Add<NomeDoServico>Module, que expõe seu próprio DbContext
	// concreto como AppDbContext). Ver docs/0005 - Migração para Arquitetura de Microsserviços.md.
	public static IServiceCollection AddCommon(this IServiceCollection services)
	{
		services.AddScoped<IErrorLogService, ErrorLogService>();
		services.AddScoped<IEmailQueueService, EmailQueueService>();

		return services;
	}
}
