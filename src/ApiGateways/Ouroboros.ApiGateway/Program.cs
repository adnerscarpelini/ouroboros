using System.Diagnostics;
using System.Threading.RateLimiting;

// Os nomes das políticas são os mesmos usados em "RateLimiterPolicy" nas rotas do appsettings.json.
const string authSensitivePolicy = "auth-sensitive";
const string authDefaultPolicy = "auth-default";
const string correlationIdHeaderName = "X-Correlation-Id";

var builder = WebApplication.CreateBuilder(args);

builder.Services
	.AddReverseProxy()
	.LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHealthChecks();

// O gateway é a borda pública: é aqui que limite de requisição faz sentido, não dentro de cada
// serviço. O bloqueio por usuário do Auth protege uma conta específica de força bruta, mas não
// impede varrer logins diferentes, nem disparar e-mail em massa por "forgot-password".
builder.Services.AddRateLimiter(options =>
{
	options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

	options.AddPolicy(authSensitivePolicy, httpContext =>
		RateLimitPartition.GetFixedWindowLimiter(
			partitionKey: ResolveClientPartitionKey(httpContext),
			factory: _ => new FixedWindowRateLimiterOptions
			{
				PermitLimit = 10,
				Window = TimeSpan.FromMinutes(1)
			}));

	options.AddPolicy(authDefaultPolicy, httpContext =>
		RateLimitPartition.GetFixedWindowLimiter(
			partitionKey: ResolveClientPartitionKey(httpContext),
			factory: _ => new FixedWindowRateLimiterOptions
			{
				PermitLimit = 100,
				Window = TimeSpan.FromMinutes(1)
			}));
});

var app = builder.Build();

// Todo request ganha um X-Correlation-Id antes de ser encaminhado. Sem isso, cada serviço só teria
// o próprio TraceIdentifier, que é local ao processo e não serve para ligar o que aconteceu num
// serviço ao que aconteceu no outro.
app.Use(async (httpContext, next) =>
{
	var correlationId = httpContext.Request.Headers[correlationIdHeaderName].FirstOrDefault();

	if (string.IsNullOrWhiteSpace(correlationId))
	{
		correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("n");
		httpContext.Request.Headers[correlationIdHeaderName] = correlationId;
	}

	// Devolvido também na resposta, para que o cliente consiga citar o id ao relatar um problema.
	httpContext.Response.Headers[correlationIdHeaderName] = correlationId;

	await next();
});

app.UseRateLimiter();

// Antes do MapReverseProxy para que o health do próprio gateway nunca seja confundido
// com uma rota a encaminhar para um serviço.
app.MapHealthChecks("/health");

app.MapReverseProxy();

app.Run();

// Limite por IP de origem. Requisições sem IP conhecido caem todas na mesma partição — é o
// comportamento conservador: na dúvida, limita mais, não menos.
static string ResolveClientPartitionKey(HttpContext httpContext)
{
	return httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";
}
