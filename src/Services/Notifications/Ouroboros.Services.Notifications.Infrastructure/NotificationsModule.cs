using Microsoft.Extensions.DependencyInjection;
using Ouroboros.BuildingBlocks.Infrastructure;
using Ouroboros.Services.Notifications.Application;

namespace Ouroboros.Services.Notifications.Infrastructure;

public static class NotificationsModule
{
	public static IServiceCollection AddNotificationsModule(
		this IServiceCollection services,
		string connectionString,
		EmailDeliveryOptions emailDeliveryOptions,
		SmtpOptions smtpOptions
	)
	{
		services.AddSqlDatabase(connectionString);


		services.AddSingleton(emailDeliveryOptions);
		services.AddSingleton(smtpOptions);

		// Persistência: os casos de uso na Application só conhecem contratos; as implementações usam SQL.
		services.AddScoped<IUnitOfWork, UnitOfWork>();
		services.AddScoped<IEmailDeliveryRepository, EmailDeliveryRepository>();

		// Caso de uso: a porta de entrada do consumidor (mora na Application).
		services.AddScoped<IEmailDeliveryIntakeService, EmailDeliveryIntakeService>();

		services.AddScoped<IEmailTemplateRenderer, EmailTemplateRendererService>();
		services.AddScoped<IEmailSender, SmtpEmailSenderService>();

		// Entrega: roda em segundo plano, a partir do banco, sem depender do transporte.
		services.AddScoped<EmailDeliveryDispatcherService>();
		services.AddHostedService<EmailDeliveryProcessorService>();

		return services;
	}
}
