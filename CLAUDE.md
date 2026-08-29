# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## O que é este projeto

Ouroboros é um projeto pessoal de estudos contínuo (sem "fim" planejado) usando o backend de um e-commerce como produto de exemplo, para praticar Clean Architecture, DDD, Docker e segurança de aplicações ao longo do tempo.

Antes de escrever, revisar ou refatorar qualquer código C#, siga a skill [ags-developer](.claude/skills/ags-developer/SKILL.md) — ela define as regras de branch/commit, idioma, nomenclatura (casing) e formatação de assinaturas de métodos usadas neste repositório. Para criar, revisar ou ajustar testes automatizados, siga também a skill [ags-qa](.claude/skills/ags-qa/SKILL.md). Para qualquer decisão de banco de dados (schemas, migrations, nomenclatura de tabelas/colunas), siga a skill [ags-dba](.claude/skills/ags-dba/SKILL.md).

## Comandos

```bash
# build da solution inteira (executa os testes automaticamente ao final, ver Directory.Build.targets)
dotnet build

# rodar a API (perfil "http" ou "https" definido em launchSettings.json)
dotnet run --project src/Ouroboros.Api

# rodar todos os testes
dotnet test

# rodar os testes de um único projeto
dotnet test tests/BuildingBlocks/Ouroboros.BuildingBlocks.Domain.Tests

# rodar um único teste (por nome do método/classe, via filtro do xUnit)
dotnet test --filter "FullyQualifiedName~NomeDoTeste"
```

A API sobe em `http://localhost:5082` (perfil `http`) ou `https://localhost:7272` (perfil `https`).

## Arquitetura

Clean Architecture dentro de um monolito modular, com cada camada/módulo como um projeto `.csproj` separado, para que a regra de dependência seja garantida pelo compilador. Decisão completa e justificativa em [docs/0000 - Arquitetura.md](docs/0000%20-%20Arquitetura.md).

```
src/BuildingBlocks/Ouroboros.BuildingBlocks.Domain          → tipos-base de domínio compartilhados entre módulos. Sem dependências de outras camadas.
src/BuildingBlocks/Ouroboros.BuildingBlocks.Application     → abstrações de aplicação compartilhadas entre módulos. Depende de BuildingBlocks.Domain.
src/BuildingBlocks/Ouroboros.BuildingBlocks.Infrastructure  → infraestrutura de propósito geral compartilhada entre módulos. Depende de BuildingBlocks.Application.
src/Modules/<NomeDoModulo>/                                 → módulos de negócio (bounded contexts), cada um com sua própria trinca Domain/Application/Infrastructure. Ainda vazio — nenhum módulo criado.
src/Ouroboros.Api                                            → controllers, injeção de dependência, configuração HTTP. Depende dos BuildingBlocks e dos módulos.
```

A dependência flui sempre para dentro: `Api` → `Infrastructure` → `Application` → `Domain`. Nunca adicione uma referência de projeto na direção contrária (ex.: `Domain` referenciando `Infrastructure`). Um módulo de negócio nunca referencia o `Domain`/`Application` de outro módulo diretamente — só `BuildingBlocks` — ver [src/Modules/README.md](src/Modules/README.md).

Cada projeto em `src/` tem um projeto de testes xUnit correspondente em `tests/`, no mesmo agrupamento (`tests/BuildingBlocks/...` hoje). Todo serviço/caso de uso ou regra de negócio novo deve vir acompanhado do teste correspondente no projeto da mesma camada.

## Documentação

Documentos ficam em `docs/`, numerados sequencialmente em Markdown (`0000 - Arquitetura.md`, `0001 - ...`, etc.). Sempre que algo for implementado ou alterado, revise os documentos existentes e edite os que forem afetados; crie um documento novo na sequência só se nenhum existente cobrir a mudança.

## Postman

`src/Ouroboros.Api/Postman/Ouroboros.postman_collection.json` é a collection Postman do projeto (schema v2.1). Sempre que um método/endpoint da Api for criado ou alterado, revise e ajuste essa collection.

## Git

- Branch padrão de desenvolvimento: `development`. Não trabalhe diretamente na `main`.
- Nunca faça commit ou push automaticamente — apenas prepare as alterações e avise que estão prontas para revisão.
