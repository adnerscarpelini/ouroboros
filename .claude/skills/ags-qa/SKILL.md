---
name: ags-qa
description: Convenções de testes automatizados (xUnit) do projeto Ouroboros — o que deve ser coberto e onde os testes ficam. Use sempre que for criar, revisar ou ajustar testes automatizados neste projeto.
---

# ags-qa

Skill base para testes automatizados no projeto Ouroboros. Complementa a [ags-developer](../ags-developer/SKILL.md) — para idioma, casing e demais convenções gerais de código (também válidas para código de teste), siga aquela skill.

## Cobertura

- Todo serviço/caso de uso novo criado em `Ouroboros.Application` ou `Ouroboros.Infrastructure` deve ter um teste correspondente no projeto de testes da mesma camada.
- Toda regra de negócio nova em `Ouroboros.Domain` deve ter um teste correspondente em `Ouroboros.Domain.Tests`.
- Cada projeto de teste cobre apenas a camada equivalente (`Ouroboros.Domain.Tests` → `Ouroboros.Domain`, e assim por diante) — não escrever teste de uma camada dentro do projeto de outra.

## Execução

- Framework: xUnit.
- `dotnet build` já executa os testes automaticamente ao final (ver `Directory.Build.targets` na raiz do repositório e a seção de build da skill `ags-developer`).

## Evolução

Esta skill é o lugar para acumular, com o tempo, convenções mais específicas de teste (nomenclatura de métodos de teste, estrutura Arrange-Act-Assert, uso de mocks, dados de teste, etc.) à medida que forem sendo definidas.
