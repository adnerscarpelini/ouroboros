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

## Migrations

- Ferramenta: **EF Core Migrations**.
- Cada módulo com persistência tem seu próprio `DbContext` (na camada `Infrastructure` do módulo), configurado para usar apenas o schema daquele módulo, e suas próprias migrations — não existe um `DbContext` único e global para o projeto inteiro.

## Nomenclatura (casing)

- Tabelas e colunas no Postgres: **snake_case** (ex.: tabela `users`, coluna `created_at`), seguindo a convenção idiomática do Postgres.
- Entidades e propriedades em C# continuam em PascalCase (ver `ags-developer`); a conversão para snake_case no banco é automática via pacote `EFCore.NamingConventions`, não manual.

## Evolução

Esta skill é o lugar para acumular, com o tempo, convenções mais específicas (padrão de nome de chaves estrangeiras e índices, uso de tipos específicos do Postgres, estratégia de seed de dados, etc.) à medida que forem sendo definidas.
