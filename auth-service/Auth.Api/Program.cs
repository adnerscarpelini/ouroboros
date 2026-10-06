using Ouroboros.Auth.Api.Configuration;
using Ouroboros.Auth.Api.Middleware;
using Ouroboros.Auth.Infrastructure.Migrations;
using Ouroboros.Auth.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// A API nunca roda DDL (spec 2026092511). A mesma imagem, chamada com "migrate", aplica as migrations com o papel dono do
// schema (auth_migrator) e sai. No Compose, o servico auth-migrate roda antes e a API depende dele.
if (args.FirstOrDefault() == "migrate")
{
    var migrationConnectionString = builder.Configuration.GetConnectionString("Migration");

    if (string.IsNullOrWhiteSpace(migrationConnectionString))
    {
        throw new InvalidOperationException("Connection string 'Migration' is not configured.");
    }

    MigrationRunner.Run(migrationConnectionString);
    return;
}

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();

    var seqServerUrl = context.Configuration["Seq:ServerUrl"];

    if (!string.IsNullOrWhiteSpace(seqServerUrl))
    {
        configuration.WriteTo.Seq(seqServerUrl);
    }
});

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

DapperConfiguration.Configure();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddUseCases(builder.Configuration, connectionString);
builder.Services.AddJwtAuthentication();
builder.Services.AddRateLimiting(builder.Configuration);
builder.Services.AddTokenCleanup(builder.Configuration);
builder.Services.AddTrustedForwardedHeaders(builder.Configuration);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseForwardedHeaders();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.Run();

// Exposto para o WebApplicationFactory dos testes de integracao.
public partial class Program;
