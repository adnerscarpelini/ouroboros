using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Ouroboros.Services.Auth.Api;
using Ouroboros.BuildingBlocks.Application;
using Ouroboros.BuildingBlocks.Infrastructure;
using Ouroboros.Services.Auth.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
	// Marcado como "ready": entra na prontidão (a Api depende do banco para servir),
	// mas fica fora do liveness — um banco fora do ar não significa processo travado.
	.AddDbContextCheck<AuthDbContext>(name: "postgres", tags: ["ready"]);

// Esta Api fica atrás do Api Gateway. Sem ler os cabeçalhos X-Forwarded-*, ela enxergaria o IP, o scheme
// e o host do gateway no lugar dos do cliente original — o que estraga log de origem e qualquer decisão
// baseada em IP. KnownIPNetworks/KnownProxies são limpos porque em container o gateway não chega por
// loopback e não tem IP fixo; o que garante que só ele alcança esta porta é a rede, não esta lista.
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

var publicBaseUrl = builder.Configuration["App:PublicBaseUrl"]
	?? throw new InvalidOperationException("Configuração 'App:PublicBaseUrl' não definida.");

// Chave PRIVADA (assina) — só o Auth tem, nunca é compartilhada com outro serviço.
var jwtSigningKeyPem = ReadPem("Jwt:SigningKeyPem")
	?? throw new InvalidOperationException("Configuração 'Jwt:SigningKeyPem' (ou 'Jwt:SigningKeyPemPath') não definida. Ver docs/0002 - Setup do Banco de Dados Local.md.");
// Chave PÚBLICA (valida) — não é segredo, mas é específica do par gerado neste ambiente;
// qualquer serviço que só precise validar token usa só esta.
var jwtPublicKeyPem = ReadPem("Jwt:PublicKeyPem")
	?? throw new InvalidOperationException("Configuração 'Jwt:PublicKeyPem' (ou 'Jwt:PublicKeyPemPath') não definida. Ver docs/0002 - Setup do Banco de Dados Local.md.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
	?? throw new InvalidOperationException("Configuração 'Jwt:Issuer' não definida.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
	?? throw new InvalidOperationException("Configuração 'Jwt:Audience' não definida.");

var emailOutboxOptions = builder.Configuration.GetSection("EmailOutbox").Get<EmailOutboxOptions>()
	?? throw new InvalidOperationException("Seção 'EmailOutbox' não configurada.");

builder.Services.AddCommon<AuthDbContext>();
// Entrega da fila de e-mails: roda em segundo plano, fora da transação que enfileirou.
builder.Services.AddEmailOutbox<AuthDbContext>(emailOutboxOptions);
builder.Services.AddAuthModule(
	connectionString: postgresConnectionString,
	publicBaseUrl: publicBaseUrl,
	jwtSigningKeyPem: jwtSigningKeyPem,
	jwtPublicKeyPem: jwtPublicKeyPem,
	jwtIssuer: jwtIssuer,
	jwtAudience: jwtAudience
);

var jwtPublicKey = RSA.Create();
jwtPublicKey.ImportFromPem(jwtPublicKeyPem);

builder.Services
	.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidIssuer = jwtIssuer,
			ValidAudience = jwtAudience,
			IssuerSigningKey = new RsaSecurityKey(jwtPublicKey),
			ClockSkew = TimeSpan.FromMinutes(1)
		};
	});

// Todo endpoint exige autenticação por padrão, a menos que marcado explicitamente com [AllowAnonymous]
// — ver seção "Autorização de endpoints" da skill ags-developer.
builder.Services.AddAuthorizationBuilder()
	.SetFallbackPolicy(new AuthorizationPolicyBuilder()
		.RequireAuthenticatedUser()
		.Build());

var app = builder.Build();

// Precisa vir antes de qualquer middleware que leia scheme/host/IP da requisição.
app.UseForwardedHeaders();

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
}

// Sem UseHttpsRedirection aqui de propósito: o TLS termina no Api Gateway, que encaminha para esta Api
// por HTTP na rede interna. Com o redirect ligado, uma requisição vinda do gateway voltava como 307
// apontando para a porta interna do serviço (https://localhost:7271) — vazando a topologia interna
// para o cliente e quebrando o fluxo. Ver docs/0000 - Arquitetura.md, seção "API Gateway".

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// AllowAnonymous explícito: a FallbackPolicy acima exigiria token, e quem consulta o health
// (healthcheck do container, gateway, orquestrador) não tem nem como obter um.

// Liveness: o processo está de pé e respondendo? Nenhuma checagem de dependência entra aqui —
// derrubar o container porque o banco piscou só transformaria uma falha em duas.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();

// Readiness: a Api consegue mesmo atender? É o que o Compose espera antes de subir o gateway.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
	Predicate = healthCheck => healthCheck.Tags.Contains("ready")
}).AllowAnonymous();

app.Run();

// Lê um PEM de duas origens: o valor direto na configuração (User Secrets, no desenvolvimento local)
// ou o caminho de um arquivo em "<chave>Path" (secret montado pelo Docker Compose, no container).
// PEM é multilinha, o que o torna ruim de carregar em variável de ambiente.
string? ReadPem(string configurationKey)
{
	var inlinePem = builder.Configuration[configurationKey];

	if (!string.IsNullOrWhiteSpace(inlinePem))
	{
		return inlinePem;
	}

	var pemPath = builder.Configuration[$"{configurationKey}Path"];

	if (string.IsNullOrWhiteSpace(pemPath))
	{
		return null;
	}

	if (!File.Exists(pemPath))
	{
		throw new InvalidOperationException($"Configuração '{configurationKey}Path' aponta para '{pemPath}', que não existe.");
	}

	return File.ReadAllText(pemPath);
}
