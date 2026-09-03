# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## O que é este projeto

Ouroboros é um projeto pessoal de estudos contínuo (sem "fim" planejado) usando o backend de um e-commerce como produto de exemplo, para praticar Clean Architecture, DDD, Docker e segurança de aplicações ao longo do tempo.

Antes de escrever, revisar ou refatorar qualquer código C#, siga a skill [ags-developer](.claude/skills/ags-developer/SKILL.md) — ela define as regras de branch/commit, idioma, nomenclatura (casing) e formatação de assinaturas de métodos usadas neste repositório. Para criar, revisar ou ajustar testes automatizados, siga também a skill [ags-qa](.claude/skills/ags-qa/SKILL.md). Para qualquer decisão de banco de dados (schemas, migrations, nomenclatura de tabelas/colunas), siga a skill [ags-dba](.claude/skills/ags-dba/SKILL.md). Para criar, revisar ou atualizar qualquer documentação (`docs/`, incluindo fluxos desenhados em Excalidraw), siga a skill [ags-technical-writer](.claude/skills/ags-technical-writer/SKILL.md).

## Comandos

```bash
# build da solution inteira (executa os testes automaticamente ao final, ver Directory.Build.targets)
dotnet build

# rodar a API do Auth (porta interna 5081/7271 — perfil "http" ou "https" em launchSettings.json)
dotnet run --project src/Services/Auth/Ouroboros.Services.Auth.Api

# rodar o Api Gateway (porta pública 5082/7272) — precisa do Auth rodando pra ter o que rotear
dotnet run --project src/ApiGateways/Ouroboros.ApiGateway

# rodar todos os testes
dotnet test

# rodar os testes de um único projeto
dotnet test tests/BuildingBlocks/Ouroboros.BuildingBlocks.Domain.Tests

# rodar um único teste (por nome do método/classe, via filtro do xUnit)
dotnet test --filter "FullyQualifiedName~NomeDoTeste"

# subir banco + Mailpit (fluxo do dia a dia — as Apis rodam pela IDE / dotnet run)
docker compose up -d

# subir a stack inteira em container — ver docs/0006
# (e-mails de desenvolvimento em http://localhost:8025)
docker compose --profile apps up -d --build
```

O ponto de entrada público é o **Api Gateway**, em `http://localhost:5082` (perfil `http`) ou `https://localhost:7272` (perfil `https`) — é nele que a collection Postman aponta. O Auth roda numa porta interna própria (`5081`/`7271`), só para acesso direto durante desenvolvimento; em produção, só o gateway teria porta exposta.

## Arquitetura

Clean Architecture com microsserviços, cada camada/serviço como um projeto `.csproj` separado, para que a regra de dependência seja garantida pelo compilador. Decisão completa e justificativa em [docs/0000 - Arquitetura.md](docs/0000%20-%20Arquitetura.md).

```
src/BuildingBlocks/Ouroboros.BuildingBlocks.Domain           → tipos-base de domínio compartilhados entre serviços. Sem dependências de outras camadas.
src/BuildingBlocks/Ouroboros.BuildingBlocks.Application      → abstrações de aplicação compartilhadas entre serviços. Depende de BuildingBlocks.Domain.
src/BuildingBlocks/Ouroboros.BuildingBlocks.Infrastructure   → infraestrutura de propósito geral compartilhada entre serviços (código, não dado — cada serviço persiste na própria base). Depende de BuildingBlocks.Application.
src/Services/<NomeDoServico>/                                → microsserviços (bounded contexts), cada um com sua própria trinca Domain/Application/Infrastructure + um projeto Api próprio. Primeiro exemplo: Auth (src/Services/Auth/), com host HTTP em Ouroboros.Services.Auth.Api.
src/ApiGateways/Ouroboros.ApiGateway                         → ponto de entrada HTTP público (YARP). Só roteia — sem regra de negócio, sem banco, sem referência a projetos de serviço.
```

A dependência flui sempre para dentro: `Api` → `Infrastructure` → `Application` → `Domain`. Nunca adicione uma referência de projeto na direção contrária (ex.: `Domain` referenciando `Infrastructure`). Um serviço nunca referencia o `Domain`/`Application` de outro serviço diretamente — só `BuildingBlocks` — ver [src/Services/README.md](src/Services/README.md).

Dentro de cada projeto, classes são agrupadas por tipo em subpastas (`Interfaces/`, `Models/`, `Services/`, `Persistence/`, `Persistence/Repositories/`, `Options/`) — ver "Organização de pastas dentro de um projeto" em [docs/0000](docs/0000%20-%20Arquitetura.md#organização-de-pastas-dentro-de-um-projeto).

Os casos de uso ficam na camada `Application` de cada serviço (ex.: `UserRegistrationService`), e falam com o banco só por contratos que ela declara (`IUserRepository`, `IUnitOfWork`) — nunca injetando um `DbContext`. As implementações desses contratos ficam na `Infrastructure`. Ver [docs/0005 - Repositórios e Unidade de Trabalho.md](docs/0005%20-%20Repositórios%20e%20Unidade%20de%20Trabalho.md).

Cada projeto em `src/` tem um projeto de testes xUnit correspondente em `tests/`, no mesmo agrupamento (`tests/BuildingBlocks/...`, `tests/Services/Auth/...`). Todo serviço/caso de uso ou regra de negócio novo deve vir acompanhado do teste correspondente no projeto da mesma camada.

## Tratamento de erros

Não escreva `try/catch` só para logar uma exceção. Qualquer erro não tratado que chegue à Api do Auth é capturado automaticamente pelo `GlobalExceptionHandler` (`src/Services/Auth/Ouroboros.Services.Auth.Api/GlobalExceptionHandler.cs`), que registra em `Ouroboros.BuildingBlocks.Domain.ErrorLog` (schema `common`, dentro do próprio banco do serviço) via `IErrorLogService`. Só capture uma exceção quando houver algo real a fazer com ela ali (recuperar, traduzir para um erro de domínio, tentar de novo).

## Documentação

Ver skill [ags-technical-writer](.claude/skills/ags-technical-writer/SKILL.md).

## Postman

`src/Services/Auth/Ouroboros.Services.Auth.Api/Postman/Ouroboros.postman_collection.json` é a collection Postman do projeto (schema v2.1). Sempre que um método/endpoint de uma Api for criado ou alterado, revise e ajuste essa collection.

## Git

- Branch padrão de desenvolvimento: `development`. Não trabalhe diretamente na `main`.
- Nunca faça commit ou push automaticamente — apenas prepare as alterações e avise que estão prontas para revisão.
