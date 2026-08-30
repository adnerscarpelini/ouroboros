using Ouroboros.Api;
using Ouroboros.BuildingBlocks.Infrastructure;
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

builder.Services.AddBuildingBlocks(connectionString: postgresConnectionString);
builder.Services.AddAuthModule(connectionString: postgresConnectionString);

var app = builder.Build();

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
