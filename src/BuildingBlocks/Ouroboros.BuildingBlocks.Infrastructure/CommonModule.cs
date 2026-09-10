using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.BuildingBlocks.Infrastructure;

public static class CommonModule
{
	// TDbContext é o DbContext concreto do serviço chamador (ex.: AuthDbContext), exposto aqui também
	// como AppDbContext — o tipo base que ErrorLogService/OutboxMessageQueue conhecem. É assim que cada
	// serviço persiste ErrorLog/OutboxMessage na própria base sem que BuildingBlocks conheça serviço algum.
	// O parâmetro de tipo é obrigatório de propósito: antes esse registro era escrito à mão no
	// Add<NomeDoServico>Module de cada serviço e, se esquecido, o projeto compilava e só quebrava em
	// runtime dentro do GlobalExceptionHandler — mascarando o erro original. Agora é o compilador que cobra.
	// Ver docs/0000 - Arquitetura.md, seção "BuildingBlocks".
	public static IServiceCollection AddCommon<TDbContext>(this IServiceCollection services)
		where TDbContext : AppDbContext
	{
		services.AddScoped<AppDbContext>(serviceProvider => serviceProvider.GetRequiredService<TDbContext>());

		services.AddHttpContextAccessor();
		services.AddScoped<ICorrelationIdAccessor, HttpCorrelationIdAccessor>();

		services.AddScoped<IErrorLogService, ErrorLogService>();

		return services;
	}

	// Liga a outbox transacional do serviço: o contrato que os casos de uso usam para enfileirar e o
	// processo que publica o que estiver pendente. Fica separado do AddCommon porque um serviço pode
	// registrar erro sem produzir mensagem nenhuma.
	//
	// O transporte em si (IMessagePublisher) é registrado à parte, pela mensageria. Sem ele, as
	// mensagens continuam sendo gravadas e ficam pendentes — ver OutboxPublisherProcessor.
	public static IServiceCollection AddTransactionalOutbox<TDbContext>(
		this IServiceCollection services,
		OutboxProducer producer,
		OutboxOptions options
	)
		where TDbContext : AppDbContext
	{
		services.AddSingleton(producer);
		services.AddSingleton(options);
		services.AddScoped<IOutboxMessageQueue, OutboxMessageQueue>();
		// Registrado por fábrica de propósito: o transporte só passa a existir quando a mensageria for
		// configurada, e a validação de serviços que o contêiner faz na subida recusaria um construtor
		// que exige IMessagePublisher antes disso — derrubando a Api inteira por causa de um
		// componente que ainda nem deveria rodar. Quem decide se roda é o OutboxPublisherProcessor.
		services.AddScoped(serviceProvider => new OutboxPublisher<TDbContext>(
			dbContext: serviceProvider.GetRequiredService<TDbContext>(),
			messagePublisher: serviceProvider.GetRequiredService<IMessagePublisher>(),
			options: serviceProvider.GetRequiredService<OutboxOptions>(),
			logger: serviceProvider.GetRequiredService<ILogger<OutboxPublisher<TDbContext>>>()
		));
		services.AddHostedService<OutboxPublisherProcessor<TDbContext>>();

		return services;
	}
}
