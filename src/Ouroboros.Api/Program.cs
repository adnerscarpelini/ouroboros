using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ouroboros.Api;
using Ouroboros.Common.Infrastructure;
using Ouroboros.Modules.Auth.Infrastructure;

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

var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
	?? throw new InvalidOperationException("Configuração 'Jwt:SigningKey' não definida. Ver docs/0002 - Setup do Banco de Dados Local.md.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
	?? throw new InvalidOperationException("Configuração 'Jwt:Issuer' não definida.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
	?? throw new InvalidOperationException("Configuração 'Jwt:Audience' não definida.");

builder.Services.AddCommon(connectionString: postgresConnectionString);
builder.Services.AddAuthModule(
	connectionString: postgresConnectionString,
	apiBaseUrl: apiBaseUrl,
	jwtSigningKey: jwtSigningKey,
	jwtIssuer: jwtIssuer,
	jwtAudience: jwtAudience
);

builder.Services
	.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidIssuer = jwtIssuer,
			ValidAudience = jwtAudience,
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
			ClockSkew = TimeSpan.FromMinutes(1)
		};
	});

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
