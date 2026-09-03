---
name: ags-qa
description: Convenções de testes automatizados (xUnit) do projeto Ouroboros — o que deve ser coberto e onde os testes ficam. Use sempre que for criar, revisar ou ajustar testes automatizados neste projeto.
---

# ags-qa

Skill base para testes automatizados no projeto Ouroboros. Complementa a [ags-developer](../ags-developer/SKILL.md) — para idioma, casing e demais convenções gerais de código (também válidas para código de teste), siga aquela skill.

## Cobertura

- Todo caso de uso/regra de negócio novo criado numa camada `Application` ou `Infrastructure` (seja em `BuildingBlocks` ou dentro de um serviço em `src/Services/`) deve ter um teste correspondente no projeto de testes da mesma camada.
- Toda regra de negócio nova numa camada `Domain` deve ter um teste correspondente no projeto `.Domain.Tests` equivalente.
- Cada projeto de teste cobre apenas a camada/serviço equivalente (ex.: `Ouroboros.BuildingBlocks.Domain.Tests` → `Ouroboros.BuildingBlocks.Domain`) — não escrever teste de uma camada ou serviço dentro do projeto de outro.
- Teste de um serviço nunca depende de outro serviço — só de `BuildingBlocks` e do próprio serviço, seguindo a mesma regra de isolamento entre serviços (ver [src/Services/README.md](../../../src/Services/README.md)).

## Execução

- Framework: xUnit.
- `dotnet build` já executa os testes automaticamente ao final (ver `Directory.Build.targets` na raiz do repositório e a seção de build da skill `ags-developer`).
- O build da **imagem Docker** não roda testes: o `Dockerfile` passa `-p:OUROBOROS_SKIP_AUTOTEST=true`, porque `tests/` não entra no contexto da imagem. Teste é responsabilidade do build local e da CI — ver [ags-devops](../ags-devops/SKILL.md).

## Evolução

Esta skill é o lugar para acumular, com o tempo, convenções mais específicas de teste (nomenclatura de métodos de teste, estrutura Arrange-Act-Assert, uso de mocks, dados de teste, etc.) à medida que forem sendo definidas.
