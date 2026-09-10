using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
		services.AddDbContext<NotificationsDbContext>(options => options
			.UseNpgsql(connectionString)
			.UseSnakeCaseNamingConvention());

		services.AddSingleton(emailDeliveryOptions);
		services.AddSingleton(smtpOptions);

		// Persistência: os casos de uso na Application só conhecem estas interfaces, nunca o DbContext.
		services.AddScoped<IUnitOfWork, UnitOfWork>();
		services.AddScoped<IEmailDeliveryRepository, EmailDeliveryRepository>();

		// Caso de uso: a porta de entrada do consumidor (mora na Application).
		services.AddScoped<IEmailDeliveryIntakeService, EmailDeliveryIntakeService>();

		services.AddScoped<IEmailTemplateRenderer, EmailTemplateRenderer>();
		services.AddScoped<IEmailSender, SmtpEmailSender>();

		// Entrega: roda em segundo plano, a partir do banco, sem depender do transporte.
		services.AddScoped<EmailDeliveryDispatcher>();
		services.AddHostedService<EmailDeliveryProcessor>();

		return services;
	}
}
