---
name: ags-dba
description: Convenções de banco de dados do projeto Ouroboros — PostgreSQL, schemas, migrations, nomenclatura de tabelas/colunas e boas práticas. Use sempre que for criar/alterar entidades persistidas, escrever migrations, ou tomar qualquer decisão relacionada a banco de dados neste projeto.
---

# ags-dba

Skill base para tudo relacionado a banco de dados no projeto Ouroboros. Complementa a [ags-developer](../ags-developer/SKILL.md) (convenções gerais de código) e a [ags-qa](../ags-qa/SKILL.md) (testes).

## Banco de dados

- SGBD: **PostgreSQL**.
- Nome do banco: `ouroboros`.

## Schemas por módulo

- Cada módulo de negócio (`src/Modules/<NomeDoModulo>/`) tem seu próprio **schema** no Postgres, com o nome do módulo em minúsculo (ex.: módulo `Auth` → schema `auth`).
- Um módulo nunca lê nem escreve em tabela de outro schema/módulo diretamente — mesma regra de isolamento já aplicada ao código (ver [src/Modules/README.md](../../../src/Modules/README.md)), agora estendida aos dados.
- É essa separação por schema, e não a existência de bancos físicos separados, que hoje garante o isolamento — enquanto o projeto for um monolito modular, um único banco `ouroboros` hospeda todos os schemas.
- Tabelas do `Common` (não são de um módulo de negócio específico, ex.: `ErrorLog`) ficam no schema `shared`.

## Migrations

- Ferramenta: **EF Core Migrations**.
- Cada módulo com persistência tem seu próprio `DbContext` (na camada `Infrastructure` do módulo, nomeado `<NomeDoModulo>DbContext`, ex.: `AuthDbContext`), configurado para usar apenas o schema daquele módulo via `modelBuilder.HasDefaultSchema("<schema>")` em `OnModelCreating`, e suas próprias migrations — não existe um `DbContext` único e global para o projeto inteiro.
- Pacotes usados no `Infrastructure` de cada módulo com persistência: `Npgsql.EntityFrameworkCore.PostgreSQL` e `EFCore.NamingConventions`. O `Microsoft.EntityFrameworkCore.Design` (necessário pra ferramenta `dotnet ef`) fica só no projeto de entrada (`Ouroboros.Api`).

## Registro do módulo (DI)

- Cada módulo com persistência expõe um único método de extensão em `Infrastructure`, no padrão `Add<NomeDoModulo>Module(this IServiceCollection services, string connectionString)`, que registra o `DbContext` do módulo (com `UseNpgsql` + `UseSnakeCaseNamingConvention`) e os serviços de `Application` daquele módulo. A `Ouroboros.Api` só chama esse método — não registra `DbContext`/serviços de módulo diretamente no `Program.cs`.

## Segredos e connection string

- A connection string com a senha real **nunca** vai pro `appsettings.json` (esse arquivo é versionado). Local, ela fica no **User Secrets** do projeto `Ouroboros.Api` (`dotnet user-secrets`), equivalente ao papel do `.env` no Docker Compose — ver [docs/0002 - Setup do Banco de Dados Local.md](../../../docs/0002%20-%20Setup%20do%20Banco%20de%20Dados%20Local.md).

## Nomenclatura (casing)

- Tabelas e colunas no Postgres: **snake_case** (ex.: tabela `users`, coluna `created_at`), seguindo a convenção idiomática do Postgres.
- Entidades e propriedades em C# continuam em PascalCase (ver `ags-developer`); a conversão para snake_case no banco é automática via pacote `EFCore.NamingConventions`, não manual.

## Evolução

Esta skill é o lugar para acumular, com o tempo, convenções mais específicas (padrão de nome de chaves estrangeiras e índices, uso de tipos específicos do Postgres, estratégia de seed de dados, etc.) à medida que forem sendo definidas.
