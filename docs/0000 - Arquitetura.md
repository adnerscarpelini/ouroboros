# 0000 - Arquitetura

## Contexto

O Ouroboros é um projeto de estudos contínuo, sem previsão de "fim", cujo produto de exemplo é o backend de um e-commerce. Como o código vai ser revisitado e evoluído por anos, a estrutura inicial precisa favorecer manutenibilidade e crescimento organizado mais do que velocidade de entrega no curto prazo.

Este documento explica os conceitos com calma, pensando em quem vem de uma arquitetura clássica em camadas (ex.: tela → regra de negócio no servidor → banco de dados) e está vendo esses termos pela primeira vez. A ideia é que, se esquecer algum conceito lá na frente, baste reler aqui.

## Conceitos

### Clean Architecture

É uma forma de organizar o código em camadas concêntricas, onde a camada mais interna (`Domain`, as regras de negócio) não sabe nada sobre as camadas de fora (banco de dados, web, etc.). Quem depende de quem é sempre de fora pra dentro — nunca o contrário. Isso é chamado de **regra de dependência**.

Na prática, cada camada aqui é um projeto `.csproj` separado (não só uma pasta), justamente para que essa regra seja garantida pelo compilador: se alguém tentar fazer o `Domain` referenciar o `Infrastructure`, o projeto simplesmente não compila.

### As camadas, explicadas com o seu vocabulário

| No legado 3 camadas | Aqui | Papel |
|---|---|---|
| Regra de Negócio (a parte que não muda com a tecnologia) | `Domain` | Entidades e regras de negócio puras. Não sabe o que é banco de dados, HTTP ou qualquer framework. |
| Regra de Negócio (a parte que orquestra: "faz isso, depois aquilo") | `Application` | Casos de uso (ex.: "criar um usuário"). Usa o `Domain`, mas ainda não sabe como os dados são salvos. |
| Acesso ao banco / integrações externas | `Infrastructure` | Implementação de tudo que fala com o mundo de fora: banco de dados, Active Directory, e-mail, fila de mensagens, API externa, etc. |
| O "servidor" que a tela chama | `Api` | Ponto de entrada HTTP (controllers). É quem monta tudo (injeção de dependência) e expõe os endpoints. |

A diferença mais importante pro legado clássico: lá, a "Regra de Negócio" costuma ser um bloco só, onde tudo se mistura (orquestração, regra pura e até um pouco de SQL). Aqui isso é separado de propósito, e o compilador ajuda a manter separado.

### Monolito modular

**Hoje o projeto inteiro roda como uma aplicação só** (`Ouroboros.Api`), do mesmo jeito que o seu legado tem um servidor só. A diferença é que, por dentro, o código já é organizado em pedaços isolados (**módulos**), um por assunto de negócio.

Isso é diferente de **microsserviço**: microsserviço é quando cada um desses pedaços vira uma aplicação separada, rodando em processos diferentes, cada uma com seu próprio banco, se comunicando por rede. A gente **não tem isso ainda** — só deixamos a organização pronta para que, se um dia quisermos separar um pedaço em uma aplicação própria, isso seja possível sem reescrever tudo.

### Módulo (bounded context)

Um módulo é um pedaço de negócio isolado — ex.: `Auth`, `Catalog`, `ContasAReceber`. Cada módulo tem sua própria trinca `Domain`/`Application`/(`Infrastructure`, quando necessário), e **nunca** referencia o `Domain`/`Application` de outro módulo diretamente. Essa regra de isolamento é o que permite, no futuro, arrancar um módulo inteiro e transformar em um serviço separado sem descobrir que ele estava "grudado" em outro.

### Common

É código técnico compartilhado entre módulos — coisas que não são regra de negócio de ninguém específico, mas que vários módulos (ou a própria `Api`) usariam. Fica vazio até que exista uma necessidade real e compartilhada; criar conteúdo ali por antecipação seria adivinhar uma necessidade que ainda não existe.

O primeiro conteúdo real do `Common` é a captura de erros: a entidade `ErrorLog`, o contrato `IErrorLogService` e sua implementação com EF Core, persistidos no schema `common` do Postgres. Um handler global de exceções (`GlobalExceptionHandler`, na `Ouroboros.Api`) captura qualquer erro não tratado e registra ali — centralizado num único lugar, em vez de espalhado em `try/catch` pela aplicação inteira.

O segundo é a entidade `EmailMessage` (também no schema `common`): uma fila de e-mails a enviar (assunto, corpo HTML, destinatário, se já foi enviado e quando). Por enquanto é só a estrutura da fila — nenhum módulo ainda sabe entregar e-mail de verdade (SMTP); isso fica pra quando existir um serviço de envio consumindo essa fila. Fica no `Common`, e não no `Auth`, porque enviar e-mail não é uma regra de negócio de nenhum módulo específico — vários vão precisar (confirmação de cadastro, redefinição de senha, notificação de pedido, etc.).

O nome vem de arquiteturas de referência conhecidas (ex.: o eShopOnContainers, da própria Microsoft) — não é uma tecnologia nova, é só uma pasta com esse nome.

## Decisão

Adotar **Clean Architecture** dentro de um **monolito modular**, com cada camada/módulo como um projeto `.csproj` separado.

### Camadas hoje

O primeiro módulo de negócio é o `Auth`, com as três camadas (`Domain`/`Application`/`Infrastructure`) e o primeiro caso de uso real: registro de usuário (`POST /api/auth/register`), com validação de senha forte, hash Argon2id e persistência no Postgres. O que existe além dele é a base compartilhada:

| Projeto | Responsabilidade |
|---|---|
| `Ouroboros.Common.Domain` | Tipos-base de domínio compartilhados entre módulos. Não depende de nenhuma outra camada nem de frameworks externos. |
| `Ouroboros.Common.Application` | Abstrações de aplicação compartilhadas entre módulos. Depende apenas de `Common.Domain`. |
| `Ouroboros.Common.Infrastructure` | Infraestrutura de propósito geral compartilhada entre módulos. Depende de `Common.Application`. |
| `Ouroboros.Api` | Ponto de entrada HTTP: controllers, injeção de dependência, configuração. Depende do `Common` e, futuramente, dos módulos de negócio. |

A regra de dependência flui sempre para dentro: `Api` → `Infrastructure` → `Application` → `Domain`.

### Testes

Cada camada de `src/` tem um projeto de testes correspondente em `tests/`, no mesmo agrupamento (`Common/`, e um por módulo dentro de `Modules/`), usando xUnit.

## Módulos e preparo para microsserviços

Módulos de negócio ficam em `src/Modules/<NomeDoModulo>/`, cada um com sua própria trinca `Domain`/`Application`/`Infrastructure`, isolado dos demais módulos — ver [src/Modules/README.md](../src/Modules/README.md) para a convenção e a regra de isolamento entre módulos.

Um módulo pode depender de `Common`, mas nunca do `Domain`/`Application` de outro módulo diretamente. É essa regra — não a estrutura de pastas em si — que mantém a possibilidade real de, mais adiante, extrair um módulo para um serviço/repositório próprio.

Por enquanto a `Ouroboros.Api` continua sendo um único host para todos os módulos (monolito modular). Dividir a API em serviços separados por módulo é uma decisão que pode ser tomada depois, e não muda a organização interna dos módulos quando isso acontecer.

## Estrutura de pastas

```
ouroboros/
├── src/
│   ├── Common/
│   │   ├── Ouroboros.Common.Domain/
│   │   ├── Ouroboros.Common.Application/
│   │   └── Ouroboros.Common.Infrastructure/
│   ├── Modules/
│   │   └── Auth/          → Domain + Application + Infrastructure
│   └── Ouroboros.Api/
├── tests/
│   ├── Common/
│   │   ├── Ouroboros.Common.Domain.Tests/
│   │   ├── Ouroboros.Common.Application.Tests/
│   │   └── Ouroboros.Common.Infrastructure.Tests/
│   ├── Modules/
│   │   └── Auth/
│   └── Ouroboros.Api.Tests/
├── docs/
└── Ouroboros.slnx
```

## Convenções de código

As convenções de nomenclatura, idioma, formatação e fluxo de trabalho com Git usadas neste projeto estão documentadas na skill [ags-developer](../.claude/skills/ags-developer/SKILL.md).

## Consequências

- Mais arquivos de projeto para gerenciar desde o início, comparado a uma solution única.
- Erros de dependência incorreta entre camadas (ex.: `Domain` tentando referenciar `Infrastructure`) aparecem como erro de compilação, não como revisão manual de código.
- Estrutura preparada para crescer: novos módulos de negócio entram em `src/Modules/` sem reestruturar o que já existe, e a regra de isolamento entre módulos deixa a porta aberta para, futuramente, extrair um módulo como serviço independente.
