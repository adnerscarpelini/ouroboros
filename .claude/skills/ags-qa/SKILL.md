---
name: ags-qa
description: Convenções de testes automatizados (xUnit) do projeto Ouroboros — o que deve ser coberto e onde os testes ficam. Use sempre que for criar, revisar ou ajustar testes automatizados neste projeto.
---

# ags-qa

## Specs como histórias de trabalho

- Antes de criar ou alterar testes para uma nova feature, procure em `specs/` a spec correspondente e pergunte ao usuário se ela já existe.
- Se não existir, crie a spec antes do desenvolvimento; se existir, use-a como história/item de trabalho e atualize-a conforme surgirem mudanças de escopo, cobertura ou critérios de aceite. Use `AAAAMMDDHHMMSS-Descricao.md`, com `title` e `state` (`new`, `in progress` ou `done`) no cabeçalho YAML. Specs históricas mantêm seus nomes originais.
- Ao sugerir uma mensagem de commit, inclua a data/número da spec correspondente.

Skill base para testes automatizados no projeto Ouroboros. Complementa a [ags-developer](../ags-developer/SKILL.md) — para idioma, casing e demais convenções gerais de código (também válidas para código de teste), siga aquela skill.

## Cobertura

- Todo caso de uso/regra de negócio novo criado numa camada `Application` ou `Infrastructure` (seja em `BuildingBlocks` ou dentro de um serviço em `src/Services/`) deve ter um teste correspondente no projeto de testes da mesma camada.
- Toda regra de negócio nova numa camada `Domain` deve ter um teste correspondente no projeto `.Domain.Tests` equivalente.
- Cada projeto de teste cobre apenas a camada/serviço equivalente (ex.: `Ouroboros.BuildingBlocks.Domain.Tests` → `Ouroboros.BuildingBlocks.Domain`) — não escrever teste de uma camada ou serviço dentro do projeto de outro.
- Teste de um serviço nunca depende de outro serviço — só de `BuildingBlocks` e do próprio serviço, seguindo a mesma regra de isolamento entre serviços (ver [src/Services/README.md](../../../src/Services/README.md)).

## Execução

- Framework: xUnit.
- `dotnet build` já executa os testes automaticamente ao final (ver `Directory.Build.targets` na raiz do repositório e a seção de build da skill `ags-developer`), mas só os testes rápidos — ver "Testes de integração" abaixo.
- O build da **imagem Docker** não roda testes: o `Dockerfile` passa `-p:OUROBOROS_SKIP_AUTOTEST=true`, porque `tests/` não entra no contexto da imagem. Teste é responsabilidade do build local e da CI — ver [ags-devops](../ags-devops/SKILL.md).

## Testes de integração (Postgres real)

- Toda regra que só a `Infrastructure` sabe — repositório, `UnitOfWork`/`DbSession`, o `MigrationRunner` — precisa de teste de integração contra Postgres real, não só de unitário com fake. É o que garante que o SQL escrito à mão faz o que o mapeamento e o `Rehydrate` esperam.
- Esses testes usam **Testcontainers** (`Testcontainers.PostgreSql`): cada fixture sobe um Postgres efêmero, roda as migrations reais do serviço com o mesmo `MigrationRunner`/`MigrationLoader` do `Ouroboros.DatabaseMigrator` (`Ouroboros.BuildingBlocks.Infrastructure.Migrations`), e os testes rodam contra esse banco descartável.
- Toda classe de teste de integração leva `[Trait("Category", "Integration")]`. Isso é o que separa esses testes do `dotnet build` automático — ver `RunTestsAfterBuild` em `Directory.Build.targets`, que roda com `--filter "Category!=Integration"`. Rode os de integração manualmente com `dotnet test --filter "Category=Integration"` (exige Docker ativo).
- Quando vários testes da mesma classe/coleção compartilham o mesmo container (via `ICollectionFixture`, como em `AuthDatabaseFixture`), cada teste usa dado próprio (login/e-mail com `Guid.NewGuid()`, por exemplo) para não colidir com os outros — o schema não é resetado entre testes, só o container inteiro é descartado ao final da coleção.
- Exemplo de referência: `tests/Services/AuthService/Ouroboros.AuthService.Infrastructure.Tests/Integration/` (fixture, repositório e atomicidade do `UnitOfWork`) e `tests/BuildingBlocks/Ouroboros.BuildingBlocks.Infrastructure.Tests/Migrations/` (`MigrationRunner` contra Postgres real, `MigrationLoader` como teste unitário puro).

## Evolução

Esta skill é o lugar para acumular, com o tempo, convenções mais específicas de teste (nomenclatura de métodos de teste, estrutura Arrange-Act-Assert, uso de mocks, dados de teste, etc.) à medida que forem sendo definidas.
