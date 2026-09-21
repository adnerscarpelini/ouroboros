# Arquitetura

Ouroboros é um monorepo de estudo pra praticar microsserviços com Clean Architecture (Robert C. Martin) em .NET/C#.

## Stack

- .NET 10
- ASP.NET Core — usado **só** na camada de API de cada serviço
- Solution única `Ouroboros.slnx` (formato novo do .NET 10) na raiz, agregando os projetos de todos os serviços
- xUnit
- Sem ORM completo (nada de Entity Framework Core com change tracking, nem NHibernate) — persistência via Dapper
- PostgreSQL — sobe via `docker-compose.yml` na raiz (ver `docs/0002 - Docker.md`); `auth-service` será o primeiro serviço a usar, banco `ouroboros_auth` já criado

## A regra de dependência

Dependências de código só podem apontar pra dentro, em direção ao domínio:

```
{Servico}.Api  --->  {Servico}.Infrastructure  --->  {Servico}.Application  --->  {Servico}.Domain
   |                                                        ^
   +--------------------------------------------------------+
   (a Api também depende direto de Application e Domain pra montar os serviços)
```

- `Domain` não sabe que `Application` existe. `Application` não sabe que `Infrastructure` ou `Api` existem.
- Frameworks (ASP.NET Core, Dapper, Npgsql) ficam sempre na borda externa (`Infrastructure`/`Api`), nunca no centro.
- Isso permite trocar banco de dados ou framework web no futuro sem tocar em regra de negócio.

## Os 4 projetos de cada serviço

Todo microsserviço (`<servico>-service/`) é dividido em 4 projetos `.csproj`, namespace raiz `Ouroboros.{Servico}`:

| Projeto | Depende de | Contém |
|---|---|---|
| `{Servico}.Domain` | nada | Entities, Exceptions |
| `{Servico}.Application` | `Domain` | UseCases, Gateways (interfaces) |
| `{Servico}.Infrastructure` | `Application`, `Domain` | implementação dos gateways (persistência) |
| `{Servico}.Api` | todos os anteriores | Controllers, `Program.cs`, `UseCaseConfiguration` |

Regras por projeto:

- **Domain** — entidades criadas por construtor privado + método estático `Create(...)`, que valida e lança `DomainException` quando inválido. Toda entidade persistida estende uma classe base `Entity` (id interno, `ExternalId`, `CreatedAt`, `UpdatedAt`), declarada localmente em cada serviço.
- **Application** — cada caso de uso mora em `UseCases/{Nome}/` com 4 tipos: `I{Nome}UseCase` (interface), `{Nome}Request`/`{Nome}Response` (records), `{Nome}Interactor` (implementação). A entidade de domínio nunca sai da application; quem chama um caso de uso só vê Request/Response. Métodos são assíncronos (`Task`/`Task<T>`, sufixo `Async`).
- **Infrastructure** — implementa os gateways da application. Um serviço sem banco ainda configurado guarda dados num `ConcurrentDictionary` marcado com `TODO`, com o SQL planejado comentado; assim que o banco entra, usa SQL nativo via Dapper sobre `NpgsqlConnection` — nunca ORM completo.
- **Api** — único projeto com ASP.NET Core. Uma classe estática `UseCaseConfiguration` monta manualmente, via extensão de `IServiceCollection`, os repositórios e casos de uso — os outros 3 projetos não têm nenhuma dependência do ASP.NET Core.

## Regras que nunca podem ser quebradas

1. Nenhuma referência a `Microsoft.AspNetCore.*` fora de `{Servico}.Api`.
2. Sem ORM completo em nenhum projeto — Dapper conta como SQL explícito, não como o ORM proibido por esta regra.
3. A entidade de domínio nunca atravessa a fronteira da application.
4. Serviços nunca dependem uns dos outros via `ProjectReference` — cada `<servico>-service/` é um conjunto de projetos isolado.
5. O `Ouroboros.slnx` raiz é só um agregador, sem `Directory.Build.props`/`Directory.Packages.props` compartilhado entre serviços.

## Banco de dados

- SGBD: PostgreSQL, uma única instância compartilhada entre serviços.
- Cada serviço terá seu próprio banco lógico: `ouroboros_<servico>` (ex.: `auth-service` → `ouroboros_auth`), com role própria — isso isola os serviços entre si.
- Tabelas de negócio ficam no schema `<servico>` (ex.: `auth.users`).
- Migrations são arquivos `.sql` escritos à mão, aplicadas com DbUp, nomeadas `V<AAAAMMDDHHMMSS>__Descricao.sql`, guardadas em `{Servico}.Infrastructure/Migrations/` (embutidas no assembly). Detalhes na skill `ouroboros-dba`.
- Uso do Docker (comandos, como cada serviço ganha seu próprio banco) em `docs/0002 - Docker.md`.

## Serviços existentes

Nenhum serviço foi criado ainda. `auth-service` será o primeiro, seguindo o checklist da skill `ouroboros-dev`.

## Onde estão as convenções detalhadas

As regras completas de arquitetura, código e banco vivem nas skills do projeto, não neste documento:

- `.claude/skills/ouroboros-dev/SKILL.md` — arquitetura, convenções de código, checklists pra criar serviço/caso de uso novo.
- `.claude/skills/ouroboros-dba/SKILL.md` — convenções de banco de dados.
- `.claude/skills/ouroboros-tech-writer/SKILL.md` — convenções de documentação (este arquivo segue elas).
