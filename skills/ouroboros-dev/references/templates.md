# Templates de codigo — Ouroboros

Modelos prontos para copiar e adaptar ao criar um novo microsservico ou um novo caso de uso. Substitua os placeholders:

- `{servico}` — nome do servico em minusculas (ex.: `orders`)
- `{Servico}` — nome do servico com inicial maiuscula (ex.: `Orders`)
- `{porta}` — porta HTTP escolhida para o servico (proxima livre depois de 8082)
- `{Entidade}` / `{entidade}` — nome da entidade de dominio (ex.: `Order` / `order`)
- `{CasoDeUso}` / `{casodeuso}` — nome do caso de uso em PascalCase / minusculas (ex.: `RegisterOrder` / `registerorder`)

Todos os exemplos abaixo sao baseados no `auth-service`, trocando `Auth`/`User`/`RegisterUser` pelos placeholders acima. Alvo de framework: `net10.0` (ajuste pra LTS em uso se for diferente no ambiente).

## 1. `Ouroboros.sln` — adicionar os projetos do novo servico

Nao ha "pom pai" por servico como na versao Java — o `.sln` unico na raiz do monorepo referencia diretamente os `.csproj` de todos os servicos. Depois de criar os 4 projetos (passos 2-5), adicione-os ao solution a partir da raiz:

```bash
dotnet sln Ouroboros.sln add {servico}-service/{Servico}.Domain/{Servico}.Domain.csproj
dotnet sln Ouroboros.sln add {servico}-service/{Servico}.Application/{Servico}.Application.csproj
dotnet sln Ouroboros.sln add {servico}-service/{Servico}.Application.Tests/{Servico}.Application.Tests.csproj
dotnet sln Ouroboros.sln add {servico}-service/{Servico}.Infrastructure/{Servico}.Infrastructure.csproj
dotnet sln Ouroboros.sln add {servico}-service/{Servico}.Api/{Servico}.Api.csproj
```

## 2. `{servico}-service/{Servico}.Domain/{Servico}.Domain.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

Sem `<PackageReference>` nenhuma — zero dependencias, nem de framework de teste (o projeto de teste e separado, ver passo 3).

## 3. `{servico}-service/{Servico}.Application/{Servico}.Application.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../{Servico}.Domain/{Servico}.Domain.csproj" />
  </ItemGroup>

</Project>
```

`{servico}-service/{Servico}.Application.Tests/{Servico}.Application.Tests.csproj` (projeto de teste separado — xUnit nunca entra no projeto principal):

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../{Servico}.Application/{Servico}.Application.csproj" />
  </ItemGroup>

</Project>
```

## 4. `{servico}-service/{Servico}.Infrastructure/{Servico}.Infrastructure.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../{Servico}.Application/{Servico}.Application.csproj" />
    <ProjectReference Include="../{Servico}.Domain/{Servico}.Domain.csproj" />
  </ItemGroup>

</Project>
```

> Enquanto a persistencia ainda for o `ConcurrentDictionary` temporario (ver item 10), este `.csproj` fica assim, sem pacotes NuGet. `Dapper`/`Npgsql`/`dbup-postgresql` so entram quando o banco for de fato plugado.

## 5. `{servico}-service/{Servico}.Api/{Servico}.Api.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../{Servico}.Domain/{Servico}.Domain.csproj" />
    <ProjectReference Include="../{Servico}.Application/{Servico}.Application.csproj" />
    <ProjectReference Include="../{Servico}.Infrastructure/{Servico}.Infrastructure.csproj" />
  </ItemGroup>

</Project>
```

## 6. Entidade de dominio

Toda entidade persistida estende a classe base `Entity` (id interno, external id, created at, updated at — ver [ouroboros-dba](../../ouroboros-dba/SKILL.md)), declarada localmente no `{Servico}.Domain` (nao e compartilhada entre servicos):

`{servico}-service/{Servico}.Domain/Entities/Entity.cs`

```csharp
namespace Ouroboros.{Servico}.Domain.Entities;

public abstract class Entity
{
    public long Id { get; private set; }

    public Guid ExternalId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    protected Entity()
    {
        ExternalId = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    protected void RestorePersistence(long id, Guid externalId, DateTimeOffset createdAt, DateTimeOffset? updatedAt)
    {
        Id = id;
        ExternalId = externalId;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public void MarkAsUpdated()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

`{servico}-service/{Servico}.Domain/Entities/{Entidade}.cs`

```csharp
namespace Ouroboros.{Servico}.Domain.Entities;

using Ouroboros.{Servico}.Domain.Exceptions;

public sealed class {Entidade} : Entity
{
    public string Name { get; private set; } = null!;

    private {Entidade}()
    {
    }

    public static {Entidade} Create(string name)
    {
        var entity = new {Entidade}
        {
            Name = ValidateName(name),
        };

        return entity;
    }

    public static {Entidade} Rehydrate(long id, Guid externalId, DateTimeOffset createdAt, DateTimeOffset? updatedAt, string name)
    {
        var entity = new {Entidade}
        {
            Name = name,
        };

        entity.RestorePersistence(id, externalId, createdAt, updatedAt);

        return entity;
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Name is required");
        }

        return name.Trim();
    }
}
```

`{servico}-service/{Servico}.Domain/Exceptions/DomainException.cs`

```csharp
namespace Ouroboros.{Servico}.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
```

## 7. Gateway

`{servico}-service/{Servico}.Application/Gateways/I{Entidade}Repository.cs`

```csharp
namespace Ouroboros.{Servico}.Application.Gateways;

using Ouroboros.{Servico}.Domain.Entities;

public interface I{Entidade}Repository
{
    Task AddAsync({Entidade} entity);
}
```

## 8. Caso de uso (Use Case Interactor)

`{servico}-service/{Servico}.Application/UseCases/{casodeuso}/{CasoDeUso}Request.cs`

```csharp
namespace Ouroboros.{Servico}.Application.UseCases.{CasoDeUso};

public record {CasoDeUso}Request(string Name);
```

`{CasoDeUso}Response.cs`

```csharp
namespace Ouroboros.{Servico}.Application.UseCases.{CasoDeUso};

public record {CasoDeUso}Response(Guid Id, string Name);
```

`I{CasoDeUso}UseCase.cs` (Input Boundary)

```csharp
namespace Ouroboros.{Servico}.Application.UseCases.{CasoDeUso};

public interface I{CasoDeUso}UseCase
{
    Task<{CasoDeUso}Response> ExecuteAsync({CasoDeUso}Request request);
}
```

`{CasoDeUso}Interactor.cs`

```csharp
namespace Ouroboros.{Servico}.Application.UseCases.{CasoDeUso};

using Ouroboros.{Servico}.Application.Gateways;
using Ouroboros.{Servico}.Domain.Entities;

public sealed class {CasoDeUso}Interactor : I{CasoDeUso}UseCase
{
    private readonly I{Entidade}Repository _repository;

    public {CasoDeUso}Interactor(I{Entidade}Repository repository)
    {
        _repository = repository;
    }

    public async Task<{CasoDeUso}Response> ExecuteAsync({CasoDeUso}Request request)
    {
        var entity = {Entidade}.Create(request.Name);

        await _repository.AddAsync(entity);

        return new {CasoDeUso}Response(entity.ExternalId, entity.Name);
    }
}
```

## 9. Teste do Interactor (repositorio fake, sem Moq/NSubstitute)

`{servico}-service/{Servico}.Application.Tests/UseCases/{casodeuso}/{CasoDeUso}InteractorTests.cs`

```csharp
namespace Ouroboros.{Servico}.Application.UseCases.{CasoDeUso};

using Ouroboros.{Servico}.Application.Gateways;
using Ouroboros.{Servico}.Domain.Entities;
using Ouroboros.{Servico}.Domain.Exceptions;
using Xunit;

public class {CasoDeUso}InteractorTests
{
    private sealed class Fake{Entidade}Repository : I{Entidade}Repository
    {
        public List<{Entidade}> Items { get; } = new();

        public Task AddAsync({Entidade} entity)
        {
            Items.Add(entity);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ShouldRegisterWhenNameIsValid()
    {
        var repository = new Fake{Entidade}Repository();
        var interactor = new {CasoDeUso}Interactor(repository);

        var response = await interactor.ExecuteAsync(new {CasoDeUso}Request("Example"));

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal("Example", response.Name);
        Assert.Single(repository.Items);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenNameIsInvalid()
    {
        var repository = new Fake{Entidade}Repository();
        var interactor = new {CasoDeUso}Interactor(repository);

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new {CasoDeUso}Request("   ")));
        Assert.Empty(repository.Items);
    }
}
```

## 10. Implementacao de infraestrutura (sem banco ainda)

`{servico}-service/{Servico}.Infrastructure/Persistence/Dapper{Entidade}Repository.cs`

```csharp
namespace Ouroboros.{Servico}.Infrastructure.Persistence;

using System.Collections.Concurrent;
using Ouroboros.{Servico}.Application.Gateways;
using Ouroboros.{Servico}.Domain.Entities;

public class Dapper{Entidade}Repository : I{Entidade}Repository
{
    // TODO: remover este armazenamento em memoria quando o Dapper (SQL nativo) for implementado
    private readonly ConcurrentDictionary<Guid, {Entidade}> _storage = new();

    public Task AddAsync({Entidade} entity)
    {
        // TODO: implementar com Dapper
        // INSERT INTO {servico}.{entidade}s (external_id, created_at, updated_at, name) VALUES (@ExternalId, @CreatedAt, @UpdatedAt, @Name)
        _storage[entity.ExternalId] = entity;
        return Task.CompletedTask;
    }
}
```

## 11. Camada API (ASP.NET Core)

`{servico}-service/{Servico}.Api/Program.cs`

```csharp
using Ouroboros.{Servico}.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddUseCases();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();
```

`{servico}-service/{Servico}.Api/Configuration/UseCaseConfiguration.cs`

```csharp
namespace Ouroboros.{Servico}.Api.Configuration;

using Ouroboros.{Servico}.Application.Gateways;
using Ouroboros.{Servico}.Application.UseCases.{CasoDeUso};
using Ouroboros.{Servico}.Infrastructure.Persistence;

public static class UseCaseConfiguration
{
    public static IServiceCollection AddUseCases(this IServiceCollection services)
    {
        services.AddScoped<I{Entidade}Repository, Dapper{Entidade}Repository>();
        services.AddScoped<I{CasoDeUso}UseCase, {CasoDeUso}Interactor>();

        return services;
    }
}
```

`{servico}-service/{Servico}.Api/Controllers/{Entidade}Controller.cs`

```csharp
namespace Ouroboros.{Servico}.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Ouroboros.{Servico}.Application.UseCases.{CasoDeUso};
using Ouroboros.{Servico}.Domain.Exceptions;

[ApiController]
[Route("api/{entidade}s")]
public sealed class {Entidade}Controller : ControllerBase
{
    private readonly I{CasoDeUso}UseCase _{casodeuso}UseCase;

    public {Entidade}Controller(I{CasoDeUso}UseCase {casodeuso}UseCase)
    {
        _{casodeuso}UseCase = {casodeuso}UseCase;
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] {CasoDeUso}Request request)
    {
        try
        {
            var response = await _{casodeuso}UseCase.ExecuteAsync(request);
            return Created($"/api/{entidade}s/{response.Id}", response);
        }
        catch (DomainException e)
        {
            return BadRequest(new { error = e.Message });
        }
    }
}
```

`{servico}-service/{Servico}.Api/appsettings.json`

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://+:{porta}"
      }
    }
  },
  "ConnectionStrings": {
    "Default": ""
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

`{servico}-service/{Servico}.Api/appsettings.Development.json` — a config comum ja liga o Swagger em `Program.cs` via `IsDevelopment()`, entao este arquivo so precisa existir para elevar o nivel de log local se necessario (o padrao do template do ASP.NET Core ja cobre isso):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  }
}
```

Ambiente ativado via `ASPNETCORE_ENVIRONMENT=Development` (o `docker-compose.yml` da raiz ja usa isso por padrao pro ambiente local, equivalente ao antigo `SPRING_PROFILES_ACTIVE=dev`).
