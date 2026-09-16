using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Ouroboros.BuildingBlocks.Infrastructure;
using Ouroboros.Services.Notifications.Api;
using Ouroboros.Services.Notifications.Application;
using Ouroboros.Services.Notifications.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
	// Marcado como "ready": entra na prontidão (o serviço depende do banco para aceitar e entregar
	// solicitações), mas fica fora do liveness — um banco fora do ar não significa processo travado.
		.AddCheck<SqlHealthCheck>(name: "postgres", tags: ["ready"]);

// SMTP fora do ar não entra na prontidão de propósito: as solicitações continuam sendo aceitas e
// persistidas, e a entrega retoma sozinha depois. Marcar o serviço como não-pronto por causa disso
// transformaria uma degradação de entrega numa indisponibilidade.

// Este serviço fica atrás do Api Gateway. Sem ler os cabeçalhos X-Forwarded-*, ele enxergaria o IP, o
// scheme e o host do gateway no lugar dos do cliente original.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
	options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
		| ForwardedHeaders.XForwardedProto
		| ForwardedHeaders.XForwardedHost;

	options.KnownIPNetworks.Clear();
	options.KnownProxies.Clear();
});

var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres")
	?? throw new InvalidOperationException("Connection string 'Postgres' não configurada. Ver docs/0002 - Setup do Banco de Dados Local.md.");

var emailDeliveryOptions = builder.Configuration.GetSection("EmailDelivery").Get<EmailDeliveryOptions>()
	?? throw new InvalidOperationException("Seção 'EmailDelivery' não configurada.");

var smtpOptions = builder.Configuration.GetSection("Smtp").Get<SmtpOptions>()
	?? throw new InvalidOperationException("Seção 'Smtp' não configurada.");

builder.Services.AddCommon();
builder.Services.AddNotificationsModule(
	connectionString: postgresConnectionString,
	emailDeliveryOptions: emailDeliveryOptions,
	smtpOptions: smtpOptions
);

// O consumidor de mensageria entra aqui quando o transporte existir: ele lê a fila e chama
// IEmailDeliveryIntakeService, confirmando o consumo só depois do commit local. Enquanto isso, o
// serviço já entrega tudo que estiver persistido.

var app = builder.Build();

// Precisa vir antes de qualquer middleware que leia scheme/host/IP da requisição.
app.UseForwardedHeaders();

app.UseExceptionHandler();

// Sem UseHttpsRedirection aqui de propósito: o TLS termina no Api Gateway, que alcança este serviço
// por HTTP na rede interna. Ver docs/0000 - Arquitetura.md, seção "API Gateway".

// Este host não expõe endpoint de negócio: solicitação de e-mail chega por mensageria, não por HTTP.
// Só os dois health checks respondem. O primeiro endpoint de consulta ou reprocessamento precisa
// definir sua autorização antes de existir — é decisão pendente na spec, não omissão.

// Liveness: o processo está de pé e respondendo? Nenhuma checagem de dependência entra aqui —
// derrubar o container porque o banco piscou só transformaria uma falha em duas.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });

// Readiness: o serviço consegue mesmo atender? É o que o Compose espera antes de considerá-lo pronto.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
	Predicate = healthCheck => healthCheck.Tags.Contains("ready")
});

app.Run();
