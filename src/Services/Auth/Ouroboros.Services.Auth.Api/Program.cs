using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
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

var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres")
	?? throw new InvalidOperationException("Connection string 'Postgres' não configurada. Ver docs/0002 - Setup do Banco de Dados Local.md.");

var apiBaseUrl = builder.Configuration["App:BaseUrl"]
	?? throw new InvalidOperationException("Configuração 'App:BaseUrl' não definida.");

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

builder.Services.AddCommon();
builder.Services.AddAuthModule(
	connectionString: postgresConnectionString,
	apiBaseUrl: apiBaseUrl,
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

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
