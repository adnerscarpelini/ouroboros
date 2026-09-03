using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Ouroboros.Services.Auth.Api;
using Ouroboros.BuildingBlocks.Infrastructure;
using Ouroboros.Services.Auth.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

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

// Chave PRIVADA (assina) — só o Auth tem, fica em User Secrets, nunca é compartilhada com outro serviço.
var jwtSigningKeyPem = builder.Configuration["Jwt:SigningKeyPem"]
	?? throw new InvalidOperationException("Configuração 'Jwt:SigningKeyPem' não definida. Ver docs/0002 - Setup do Banco de Dados Local.md.");
// Chave PÚBLICA (valida) — não é segredo, mas ainda fica em User Secrets aqui porque é específica
// do par de chaves gerado nesta máquina; qualquer serviço que só precise validar token usa só esta.
var jwtPublicKeyPem = builder.Configuration["Jwt:PublicKeyPem"]
	?? throw new InvalidOperationException("Configuração 'Jwt:PublicKeyPem' não definida. Ver docs/0002 - Setup do Banco de Dados Local.md.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
	?? throw new InvalidOperationException("Configuração 'Jwt:Issuer' não definida.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
	?? throw new InvalidOperationException("Configuração 'Jwt:Audience' não definida.");

builder.Services.AddCommon<AuthDbContext>();
builder.Services.AddAuthModule(
	connectionString: postgresConnectionString,
	publicBaseUrl: publicBaseUrl,
	jwtSigningKeyPem: jwtSigningKeyPem,
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

app.Run();
