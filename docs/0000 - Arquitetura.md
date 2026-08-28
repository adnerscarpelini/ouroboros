# 0000 - Arquitetura

## Contexto

O Ouroboros é um projeto de estudos contínuo, sem previsão de "fim", cujo produto de exemplo é o backend de um e-commerce. Como o código vai ser revisitado e evoluído por anos, a estrutura inicial precisa favorecer manutenibilidade e crescimento organizado mais do que velocidade de entrega no curto prazo.

## Decisão

Adotar **Clean Architecture**, com cada camada implementada como um projeto `.csproj` separado (não apenas pastas dentro de um único projeto), de forma que a regra de dependência seja garantida pelo próprio compilador e não apenas por disciplina da equipe.

### Camadas

| Projeto | Responsabilidade |
|---|---|
| `Ouroboros.Domain` | Entidades, value objects e regras de negócio puras. Não depende de nenhuma outra camada nem de frameworks externos. |
| `Ouroboros.Application` | Casos de uso, interfaces (contratos) e DTOs. Depende apenas de `Domain`. |
| `Ouroboros.Infrastructure` | Implementação dos contratos definidos em `Application`: acesso a banco de dados, integrações externas, etc. Depende de `Application`. |
| `Ouroboros.Api` | Ponto de entrada HTTP: controllers, injeção de dependência, configuração. Depende de `Application` e `Infrastructure`. |

A regra de dependência flui sempre para dentro: `Api` → `Infrastructure` → `Application` → `Domain`. O `Domain` nunca conhece as camadas externas.

### Testes

Cada camada de `src/` tem um projeto de testes correspondente em `tests/`, usando xUnit:

- `Ouroboros.Domain.Tests`
- `Ouroboros.Application.Tests`
- `Ouroboros.Infrastructure.Tests`

## Estrutura de pastas

```
ouroboros/
├── src/
│   ├── Ouroboros.Domain/
│   ├── Ouroboros.Application/
│   ├── Ouroboros.Infrastructure/
│   └── Ouroboros.Api/
├── tests/
│   ├── Ouroboros.Domain.Tests/
│   ├── Ouroboros.Application.Tests/
│   └── Ouroboros.Infrastructure.Tests/
├── docs/
└── Ouroboros.sln
```

## Convenções de código

As convenções de nomenclatura, idioma, formatação e fluxo de trabalho com Git usadas neste projeto estão documentadas na skill [ags-developer](../.claude/skills/ags-developer/SKILL.md).

## Consequências

- Mais arquivos de projeto para gerenciar desde o início, comparado a uma solution única.
- Erros de dependência incorreta entre camadas (ex.: `Domain` tentando referenciar `Infrastructure`) aparecem como erro de compilação, não como revisão manual de código.
- Estrutura preparada para crescer: novas camadas (ex.: um projeto `Ouroboros.Worker` para processamento assíncrono) podem ser adicionadas depois sem reestruturar o que já existe.
