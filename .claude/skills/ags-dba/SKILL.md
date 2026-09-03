---
name: ags-dba
description: Convenções de banco de dados do projeto Ouroboros — PostgreSQL, schemas, migrations, nomenclatura de tabelas/colunas e boas práticas. Use sempre que for criar/alterar entidades persistidas, escrever migrations, ou tomar qualquer decisão relacionada a banco de dados neste projeto.
---

# ags-dba

Skill base para tudo relacionado a banco de dados no projeto Ouroboros. Complementa a [ags-developer](../ags-developer/SKILL.md) (convenções gerais de código), a [ags-qa](../ags-qa/SKILL.md) (testes) e a [ags-devops](../ags-devops/SKILL.md) — esta cuida do **container** que hospeda o Postgres, das portas e do Compose; aqui ficam banco, schema, migrations e nomenclatura.

## Banco de dados

- SGBD: **PostgreSQL**.
- Uma única instância Postgres compartilhada entre serviços — não um container por serviço. Ver [docs/0000 - Arquitetura.md](../../../docs/0000%20-%20Arquitetura.md#banco-de-dados).

## Banco e schema por serviço

- Cada serviço (`src/Services/<NomeDoServico>/`) tem seu próprio **banco lógico** na instância Postgres, nomeado `ouroboros_<nomedoservico>` (ex.: serviço `Auth` → banco `ouroboros_auth`), com uma *role* própria que é dona desse banco. É isso — banco físico, não só schema — que garante o isolamento entre serviços: nenhum serviço tem credencial que alcance o banco de outro.
- Dentro do próprio banco de um serviço, o schema organiza por assunto: `<nomedoservico>` pras tabelas de negócio (ex.: schema `auth`) e `common` pras tabelas técnicas vindas do `BuildingBlocks` (ex.: `ErrorLog`) — ver [docs/0000](../../../docs/0000%20-%20Arquitetura.md#buildingblocks) sobre código vs. dado compartilhado.
- Banco/role de cada serviço são criados por um script em `docker/postgres/init/` na primeira subida do container (ver [docs/0002](../../../docs/0002%20-%20Setup%20do%20Banco%20de%20Dados%20Local.md)) — nunca criados manualmente. O SQL desse script é decisão daqui; o arquivo, o container e o Compose seguem a [ags-devops](../ags-devops/SKILL.md). Scripts `.sh` rodam dentro de um container Linux e **precisam** de finais de linha LF — o `.gitattributes` da raiz garante isso.

## Migrations

- Ferramenta: **EF Core Migrations**.
- Cada serviço com persistência tem seu próprio `DbContext` (na camada `Infrastructure` do serviço, nomeado `<NomeDoServico>DbContext`, ex.: `AuthDbContext`), configurado para usar o schema daquele serviço via `modelBuilder.HasDefaultSchema("<schema>")` em `OnModelCreating`, mais o mapeamento de `BuildingBlocks` (`ApplyCommonEntities()`) pro schema `common` — e suas próprias migrations, únicas pro banco daquele serviço. Todo `DbContext` de serviço herda de `AppDbContext` (`Ouroboros.BuildingBlocks.Infrastructure`) em vez de `DbContext` diretamente — ver seção "Entidade base" abaixo.
- Pacotes usados no `Infrastructure` de cada serviço com persistência: `Npgsql.EntityFrameworkCore.PostgreSQL` e `EFCore.NamingConventions`. O `Microsoft.EntityFrameworkCore.Design` (necessário pra ferramenta `dotnet ef`) fica só no projeto `Api` daquele serviço (projeto de entrada).
- Comando pra criar/aplicar uma migration de um serviço (rodar dentro da pasta `Infrastructure` do serviço — o `Api` é irmão dela, `--startup-project` sobe só um nível):
  ```bash
  dotnet ef migrations add NomeDaMigration --startup-project ../Ouroboros.Services.<NomeDoServico>.Api --context <NomeDoServico>DbContext
  dotnet ef database update --startup-project ../Ouroboros.Services.<NomeDoServico>.Api --context <NomeDoServico>DbContext
  ```

## Registro do serviço (DI)

- Cada serviço com persistência expõe um único método de extensão em `Infrastructure`, no padrão `Add<NomeDoServico>Module(this IServiceCollection services, string connectionString, ...)`, que registra o `DbContext` do serviço (com `UseNpgsql` + `UseSnakeCaseNamingConvention`) e os serviços de `Application` daquele serviço. A `Api` do serviço só chama esse método — não registra `DbContext`/serviços diretamente no `Program.cs`.

## Segredos e connection string

- A connection string com a senha real **nunca** vai pro `appsettings.json` (esse arquivo é versionado). Local, ela fica no **User Secrets** do projeto `Api` de cada serviço (`dotnet user-secrets`), equivalente ao papel do `.env` no Docker Compose — ver [docs/0002 - Setup do Banco de Dados Local.md](../../../docs/0002%20-%20Setup%20do%20Banco%20de%20Dados%20Local.md).
- Rodando em container, a connection string chega por variável de ambiente (`ConnectionStrings__Postgres`), montada no `docker-compose.yml` a partir do `.env` — ver [ags-devops](../ags-devops/SKILL.md).

## Entidade base

Toda entidade persistida herda de `Entity` (`Ouroboros.BuildingBlocks.Domain`), que carrega quatro colunas presentes em **todas** as tabelas do sistema, sempre nessa ordem física (garantida por `HasColumnOrder` em `AppDbContext`):

1. `id` (`long` / `bigint`, identity) — chave primária interna, usada em joins e FKs. Nunca exposta pela Api.
2. `external_id` (`Guid` / `uuid`, único, gerado em `Guid.NewGuid()` na criação) — identificador público, usado em rotas/DTOs da Api. Enumeration-safe: não revela volume nem ordem de criação como um `id` sequencial exposto revelaria.
3. `created_at` (`timestamptz`, UTC) — carimbado automaticamente na criação, dentro do construtor de `Entity`.
4. `updated_at` (`timestamptz`, UTC, nullable) — `null` até a primeira alteração; carimbado **automaticamente** pelo `AppDbContext.SaveChanges`/`SaveChangesAsync` (via `ChangeTracker`, chamando `Entity.MarkAsUpdated()` em toda entidade rastreada como `Modified`). Nenhum código de domínio precisa lembrar de tocar nesse campo.

```csharp
public abstract class Entity
{
	public long Id { get; private set; }
	public Guid ExternalId { get; private set; }
	public DateTime CreatedAt { get; private set; }
	public DateTime? UpdatedAt { get; private set; }

	protected Entity()
	{
		ExternalId = Guid.NewGuid();
		CreatedAt = DateTime.UtcNow;
	}

	public void MarkAsUpdated()
	{
		UpdatedAt = DateTime.UtcNow;
	}
}
```

`AppDbContext` (também em `Ouroboros.BuildingBlocks.Infrastructure`) é a base de todo `<NomeDoServico>DbContext`: aplica o índice único em `external_id` e o `HasColumnOrder` pra qualquer entidade que herde de `Entity`, e faz o auto-stamp de `updated_at` no `SaveChanges`. Um `DbContext` de serviço só precisa herdar dele (em vez de `DbContext` puro) — o resto (schema do serviço, índices específicos como `login`/`email` do `User`, e o `ApplyCommonEntities()` do `BuildingBlocks`) continua configurado no próprio `OnModelCreating` do serviço, chamando `base.OnModelCreating(modelBuilder)` no final.

## Entidades persistidas (padrão de construtor para o EF Core)

Toda entidade que vai ser persistida (tem um `DbSet<T>` em algum `DbContext`) precisa, além do construtor público "de verdade" (com as regras de negócio) e de herdar de `Entity`, de:

- Um **construtor privado sem parâmetros**, exclusivo para o EF Core materializar a entidade a partir do banco. Como `Entity` já inicializa `ExternalId`/`CreatedAt` no seu próprio construtor sem parâmetros, esse construtor privado da entidade concreta não precisa (e não deve) repetir essa inicialização — o EF sobrescreve todas as propriedades com os valores da linha do banco logo em seguida.
- **`private set`** em toda propriedade (em vez de só `get`).

Sem isso, o EF Core não consegue reconstruir a entidade a partir de uma linha do banco — na prática, ele passa a ignorar silenciosamente as propriedades sem `set`, e elas somem da migration gerada (ou, no caso do `Id`, o erro é explícito: "requires a primary key to be defined"). Isso já aconteceu com `ErrorLog` e `User` na prática — ver os dois como referência.

```csharp
public sealed class ErrorLog : Entity
{
	public string Source { get; private set; } = null!;
	// ...

	private ErrorLog()
	{
	}

	public ErrorLog(string source, /* ... */)
	{
		Source = source;
		// ... (Id, ExternalId, CreatedAt já vêm do construtor de Entity)
	}
}
```

Propriedades de referência não-nulas (`string`, não `string?`) precisam do `= null!;` no construtor privado, pra não gerar aviso de nullable reference type — o construtor público sempre sobrescreve esse valor de verdade.

## Nomenclatura (casing)

- Tabelas e colunas no Postgres: **snake_case** (ex.: tabela `users`, coluna `created_at`), seguindo a convenção idiomática do Postgres.
- Entidades e propriedades em C# continuam em PascalCase (ver `ags-developer`); a conversão para snake_case no banco é automática via pacote `EFCore.NamingConventions`, não manual.

## Leitura pesada: Query Objects com Dapper

- Padrão default pra qualquer leitura continua sendo **EF Core** (LINQ contra o `DbContext` do serviço) — o mesmo usado pra escrita.
- Só se cria um **Query Object** com **Dapper** quando uma leitura específica for pesada de verdade (relatório/dashboard com múltiplos joins e agregações, ou uma consulta que já ficou difícil/ineficiente em LINQ). Não criar por antecipação — decisão completa em [docs/0004 - EF Core e Dapper.md](../../../docs/0004%20-%20EF%20Core%20e%20Dapper.md).
- Convenção, quando existir a necessidade:
  - Contrato em `Application/Queries/I<Nome>Query.cs`: interface com um único método `ExecuteAsync(...)`, retornando um DTO (`record`) — nunca uma entidade de `Domain`.
  - Implementação em `Infrastructure/Queries/<Nome>Query.cs`: usa Dapper, SQL escrito à mão, com o schema do serviço explícito na query (Dapper não conhece o `HasDefaultSchema` do `DbContext`).
  - Registro no mesmo `Add<NomeDoServico>Module` onde já entram os serviços: `services.AddScoped<I<Nome>Query, <Nome>Query>();`.
  - Conexão vem de `IDbConnectionFactory` (contrato em `Ouroboros.BuildingBlocks.Application`, implementação Npgsql em `Ouroboros.BuildingBlocks.Infrastructure`), reaproveitando a mesma connection string do EF Core.
  - Pacote `Dapper` entra só no `Infrastructure` do serviço que tiver o primeiro Query Object — mesma regra de "`Infrastructure` só nasce quando há algo real pra colocar lá".

```csharp
// Application/Queries/IUserLoginAttemptsReportQuery.cs
public interface IUserLoginAttemptsReportQuery
{
	Task<IReadOnlyList<UserLoginAttemptsReportItem>> ExecuteAsync(
		DateTime from,
		DateTime to,
		CancellationToken cancellationToken
	);
}

public sealed record UserLoginAttemptsReportItem(
	string Login,
	int FailedAttempts,
	DateTime? LastAttemptAt
);
```

## Evolução

Esta skill é o lugar para acumular, com o tempo, convenções mais específicas (padrão de nome de chaves estrangeiras e índices, uso de tipos específicos do Postgres, estratégia de seed de dados, etc.) à medida que forem sendo definidas.
