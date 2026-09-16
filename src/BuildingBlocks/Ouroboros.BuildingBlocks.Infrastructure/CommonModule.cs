using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.BuildingBlocks.Infrastructure;

public static class CommonModule
{
	public static IServiceCollection AddSqlDatabase(
		this IServiceCollection services,
		string connectionString)
	{
		services.AddSingleton<IDbConnectionFactory>(
			new NpgsqlConnectionFactory(connectionString));
		services.AddScoped<DbSession>();

		return services;
	}

	// Cada serviço persiste seus registros comuns no próprio banco. O BuildingBlocks conhece somente
	// a conexão SQL e os nomes físicos das tabelas, nunca modelos de persistência de outro serviço.
	// Ver docs/0000 - Arquitetura.md, seção "BuildingBlocks".
	public static IServiceCollection AddCommon(this IServiceCollection services)
	{
		services.AddHttpContextAccessor();
		services.AddScoped<ICorrelationIdAccessor, HttpCorrelationIdAccessorService>();

		services.AddScoped<IErrorLogService, ErrorLogService>();

		return services;
	}

	// Liga a outbox transacional do serviço: o contrato que os casos de uso usam para enfileirar e o
	// processo que publica o que estiver pendente. Fica separado do AddCommon porque um serviço pode
	// registrar erro sem produzir mensagem nenhuma.
	//
	// O transporte em si (IMessagePublisher) é registrado à parte, pela mensageria. Sem ele, as
	// mensagens continuam sendo gravadas e ficam pendentes — ver OutboxPublisherProcessorService.
	public static IServiceCollection AddTransactionalOutbox(
		this IServiceCollection services,
		OutboxProducer producer,
		OutboxOptions options
	)
	{
		services.AddSingleton(producer);
		services.AddSingleton(options);
	services.AddScoped<IOutboxMessageQueue, OutboxMessageQueueService>();
		// Registrado por fábrica de propósito: o transporte só passa a existir quando a mensageria for
		// configurada, e a validação de serviços que o contêiner faz na subida recusaria um construtor
		// que exige IMessagePublisher antes disso — derrubando a Api inteira por causa de um
		// componente que ainda nem deveria rodar. Quem decide se roda é o OutboxPublisherProcessorService.
		services.AddScoped(serviceProvider => new OutboxPublisherService(
			session: serviceProvider.GetRequiredService<DbSession>(),
			publisher: serviceProvider.GetRequiredService<IMessagePublisher>(),
			options: serviceProvider.GetRequiredService<OutboxOptions>(),
			logger: serviceProvider.GetRequiredService<ILogger<OutboxPublisherService>>()
		));
		services.AddHostedService<OutboxPublisherProcessorService>();

		return services;
	}
}
