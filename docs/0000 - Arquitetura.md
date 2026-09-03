# 0000 - Arquitetura

## Contexto

O Ouroboros é um projeto de estudos contínuo, sem previsão de "fim", cujo produto de exemplo é o backend de um e-commerce. Como o código vai ser revisitado e evoluído por anos, a estrutura precisa favorecer manutenibilidade e crescimento organizado mais do que velocidade de entrega no curto prazo.

Este documento explica os conceitos com calma, pensando em quem vem de uma arquitetura clássica em camadas (ex.: tela → regra de negócio no servidor → banco de dados) e está vendo esses termos pela primeira vez. A ideia é que, se esquecer algum conceito lá na frente, baste reler aqui.

## Conceitos

### Clean Architecture

É uma forma de organizar o código em camadas concêntricas, onde a camada mais interna (`Domain`, as regras de negócio) não sabe nada sobre as camadas de fora (banco de dados, web, etc.). Quem depende de quem é sempre de fora pra dentro — nunca o contrário. Isso é chamado de **regra de dependência**.

Na prática, cada camada aqui é um projeto `.csproj` separado (não só uma pasta), justamente para que essa regra seja garantida pelo compilador: se alguém tentar fazer o `Domain` referenciar o `Infrastructure`, o projeto simplesmente não compila.

### As camadas, explicadas com o seu vocabulário

| No legado 3 camadas | Aqui | Papel |
|---|---|---|
| Regra de Negócio (a parte que não muda com a tecnologia) | `Domain` | Entidades e regras de negócio puras. Não sabe o que é banco de dados, HTTP ou qualquer framework. |
| Regra de Negócio (a parte que orquestra: "faz isso, depois aquilo") | `Application` | Casos de uso (ex.: "criar um usuário"). Usa o `Domain` e fala com o banco só através de contratos (`IUserRepository`, `IUnitOfWork`) que ela mesma declara — não conhece EF Core nem nenhum outro framework de persistência. Ver [docs/0005](0005%20-%20Repositórios%20e%20Unidade%20de%20Trabalho.md). |
| Acesso ao banco / integrações externas | `Infrastructure` | Implementação de tudo que fala com o mundo de fora: banco de dados, e-mail, fila de mensagens, API externa, etc. |
| O "servidor" que a tela chama | `Api` | Ponto de entrada HTTP (controllers) daquele serviço. É quem monta tudo (injeção de dependência) e expõe os endpoints. |

A diferença mais importante pro legado clássico: lá, a "Regra de Negócio" costuma ser um bloco só, onde tudo se mistura (orquestração, regra pura e até um pouco de SQL). Aqui isso é separado de propósito, e o compilador ajuda a manter separado.

### Microsserviços

O projeto é dividido em **microsserviços**: cada contexto de negócio (ex.: `Auth`) é uma aplicação própria, com processo, banco e deploy independentes, se comunicando por rede (HTTP) através de um **API Gateway** único — nunca chamando o código ou lendo o banco de outro serviço diretamente.

Isso é diferente de um **monolito modular** (onde o código já é organizado em módulos isolados, mas tudo roda num processo só, compartilhando banco): aqui a fronteira entre serviços é física, não só uma convenção de código — reforçada por rede e banco separados, não só pelo compilador.

Hoje existe só um serviço de negócio real, o `Auth`. A estrutura é a mesma pra qualquer serviço novo (ver [src/Services/README.md](../src/Services/README.md)).

### Serviço (bounded context)

Um serviço é um pedaço de negócio isolado — ex.: `Auth`, `Cadastros`. Cada serviço tem sua própria trinca `Domain`/`Application`/`Infrastructure` mais um projeto `Api` próprio (o host HTTP dele), e **nunca** referencia o `Domain`/`Application`/`Infrastructure` de outro serviço diretamente, nem lê o banco de outro serviço. Se um serviço precisar de algo de outro, isso passa por um contrato explícito (HTTP, evento) — nunca por acoplamento direto de código ou de dados.

### BuildingBlocks

É código técnico compartilhado entre serviços — coisas que não são regra de negócio de ninguém específico, mas que vários serviços usariam. Fica vazio até que exista uma necessidade real e compartilhada; criar conteúdo ali por antecipação seria adivinhar uma necessidade que ainda não existe.

O primeiro conteúdo real do `BuildingBlocks` é a captura de erros: a entidade `ErrorLog`, o contrato `IErrorLogService` e sua implementação com EF Core. O segundo é a fila de e-mails (`EmailMessage`), implementada como **Outbox**: o caso de uso enfileira a mensagem dentro da mesma transação do dado de negócio, e um `BackgroundService` entrega depois, fora dela, por SMTP. Detalhe completo em [docs/0007 - Fila de E-mails (Outbox)](0007%20-%20Fila%20de%20E-mails%20%28Outbox%29.md).

**Importante**: `BuildingBlocks` é só código, nunca dado. Cada serviço que usa `ErrorLog`/`EmailMessage` persiste sua **própria cópia física** dessas tabelas, no schema `common` do **seu próprio banco** — não existe uma tabela `common` central compartilhada entre serviços. O mapeamento (schema, nomes de tabela) é um método de extensão reutilizável (`CommonEntityConfiguration.ApplyCommonEntities()`, em `BuildingBlocks.Infrastructure`) que cada `DbContext` de serviço chama no seu `OnModelCreating`, ao lado do que já configura pro schema de negócio dele. Código pode ser compartilhado; dados não.

O nome vem de arquiteturas de referência conhecidas (ex.: o eShopOnContainers, da própria Microsoft) — não é uma tecnologia nova, é só uma pasta com esse nome.

### API Gateway

Ponto de entrada HTTP único e público — hoje `http://localhost:5082`. Só roteia (`YARP`, configurado via `appsettings.json`): não tem regra de negócio, não acessa banco, e não tem `ProjectReference` a nenhum projeto de serviço. Cada serviço continua com sua própria porta interna (ex.: Auth em `5081`), usada só durante desenvolvimento — em produção, só o gateway teria porta exposta.

O gateway não valida nem emite JWT — cada serviço valida seus próprios tokens (ver seção "Autenticação entre serviços" abaixo). Isso evita transformar o gateway num ponto de acoplamento de identidade.

O TLS termina no gateway. As Apis de serviço **não** fazem redirecionamento para HTTPS: o gateway as alcança por HTTP na rede interna, e com o redirect ligado uma requisição vinda dele voltava como `307` apontando para a porta interna do serviço (`https://localhost:7271`) — vazando a topologia interna para o cliente. Em troca, cada Api lê os cabeçalhos `X-Forwarded-*` (`UseForwardedHeaders`) para continuar enxergando o IP, o scheme e o host originais de quem chamou.

### Autenticação entre serviços

O Auth é o único emissor de identidade: só ele cria/renova/revoga token. Qualquer outro serviço que precise aceitar sessões do Auth só **valida** o JWT — nunca emite um.

A assinatura usa um **par de chaves RSA assimétrico (RS256)**, não uma chave simétrica compartilhada: a chave privada só existe no Auth; a chave pública (não é segredo) é o que qualquer serviço validador recebe. Isso evita o problema de uma chave simétrica em múltiplos serviços — com HMAC, todo serviço que só precisa validar acabaria conhecendo o mesmo segredo capaz de assinar, e qualquer vazamento permitiria forjar token. Detalhe completo (claims, validade, geração do par de chaves) em [docs/0003 - Autenticação.md](0003%20-%20Autenticação.md) e [docs/0002 - Setup do Banco de Dados Local.md](0002%20-%20Setup%20do%20Banco%20de%20Dados%20Local.md).

## Decisão

Adotar **Clean Architecture** com **microsserviços**, cada camada/serviço como um projeto `.csproj` separado.

### Serviços hoje

O primeiro serviço de negócio é o `Auth`, com as quatro camadas (`Domain`/`Application`/`Infrastructure`/`Api`) e os casos de uso de identidade: registro, confirmação de e-mail, login, refresh token, logout e redefinição de senha (ver [docs/0003](0003%20-%20Autenticação.md)). O que existe além dele é a base compartilhada e o ponto de entrada:

| Projeto | Responsabilidade |
|---|---|
| `Ouroboros.BuildingBlocks.Domain` | Tipos-base de domínio compartilhados entre serviços. Não depende de nenhuma outra camada nem de frameworks externos. |
| `Ouroboros.BuildingBlocks.Application` | Abstrações de aplicação compartilhadas entre serviços. Depende apenas de `BuildingBlocks.Domain`. |
| `Ouroboros.BuildingBlocks.Infrastructure` | Infraestrutura de propósito geral compartilhada entre serviços — código, nunca dado. Depende de `BuildingBlocks.Application`. |
| `Ouroboros.ApiGateway` | Ponto de entrada HTTP público (YARP). Não referencia nenhum projeto de serviço. |
| `Ouroboros.Services.Auth.Api` | Host HTTP do Auth: controllers, injeção de dependência, configuração. Depende do `BuildingBlocks` e do próprio `Auth.Infrastructure`. |

A regra de dependência flui sempre para dentro: `Api` → `Infrastructure` → `Application` → `Domain`.

### Organização de pastas dentro de um projeto

Pra evitar que classes de tipos diferentes (interface, DTO/resultado, implementação, configuração de banco) fiquem misturadas soltas na raiz, cada camada agrupa por tipo em subpastas:

- **Domain**: sem subpastas — hoje só tem entidades, não há o que separar.
- **Application**: `Services/` (os casos de uso em si, ex.: `UserRegistrationService`), `Interfaces/` (contratos que o caso de uso consome, ex.: `IUserRepository`, `IUnitOfWork`), `Models/` (DTOs/resultados, ex.: `AuthenticationResult`, `Result`) e `Options/` (configuração de que o caso de uso precisa, ex.: `AuthApplicationOptions`).
- **Infrastructure**: `Persistence/` (`DbContext`, mapeamento EF Core, `UnitOfWork` e a subpasta `Repositories/` com as implementações dos contratos de persistência da Application), `Services/` (implementações concretas de contratos técnicos, ex.: `Argon2PasswordHasher`, `JwtTokenGenerator`), `Options/` (records de configuração, ex.: `JwtOptions`). O arquivo `Add<NomeDoServico>Module`/`AddCommon` fica na raiz — é a porta de entrada do projeto.
- **Testes**: fakes agrupados em `Fakes/`; os arquivos de teste em si ficam na raiz do projeto de teste.

O namespace de cada arquivo continua o mesmo (raiz do projeto) — só a pasta física muda. Isso evita ajustar `using` em cascata pela solution toda vez que um arquivo muda de pasta.

Todo serviço novo (`Cadastros`, etc.) segue essa mesma convenção desde o início.

### Testes

Cada projeto em `src/` tem um projeto de testes xUnit correspondente em `tests/`, no mesmo agrupamento (`tests/BuildingBlocks/...`, `tests/Services/Auth/...`). Todo serviço/caso de uso ou regra de negócio novo deve vir acompanhado do teste correspondente no projeto da mesma camada.

## Banco de dados

Um **banco lógico por serviço**, numa **única instância Postgres compartilhada** — bancos diferentes na mesma instância já são isolados pelo próprio Postgres (uma conexão aberta num banco não enxerga outro). Cada serviço recebe uma *role* própria, dona do seu banco; nenhum serviço usa uma credencial que alcance o banco de outro.

Um container por serviço daria isolamento de recursos (CPU/memória/IO) e permitiria versões de Postgres diferentes, mas custa mais containers rodando à toa numa máquina de desenvolvimento — banco único por serviço dentro de uma instância compartilhada é suficiente pra este projeto; um serviço que precisar de isolamento de recursos de verdade ganha sua própria instância nesse momento.

Passo a passo prático (subir o container, gerar/aplicar migrations) em [docs/0002 - Setup do Banco de Dados Local.md](0002%20-%20Setup%20do%20Banco%20de%20Dados%20Local.md).

## Estrutura de pastas

```
ouroboros/
├── docker-compose.yml
├── docker/postgres/init/         → scripts que criam banco+role de cada serviço na 1ª subida
├── docker/secrets/               → chaves usadas pelos containers (nunca versionadas)
├── src/
│   ├── ApiGateways/
│   │   └── Ouroboros.ApiGateway/        → Dockerfile próprio
│   ├── BuildingBlocks/
│   │   ├── Ouroboros.BuildingBlocks.Domain/
│   │   ├── Ouroboros.BuildingBlocks.Application/
│   │   └── Ouroboros.BuildingBlocks.Infrastructure/
│   └── Services/
│       └── Auth/
│           ├── Ouroboros.Services.Auth.Api/          → Dockerfile próprio
│           ├── Ouroboros.Services.Auth.Domain/
│           ├── Ouroboros.Services.Auth.Application/
│           └── Ouroboros.Services.Auth.Infrastructure/
├── tests/
│   ├── BuildingBlocks/
│   └── Services/
│       └── Auth/
├── docs/
└── Ouroboros.slnx
```

## Convenções de código

As convenções de nomenclatura, idioma, formatação e fluxo de trabalho com Git usadas neste projeto estão documentadas na skill [ags-developer](../.claude/skills/ags-developer/SKILL.md). As de infraestrutura — `Dockerfile`, Compose, portas, health checks e segredos — estão na skill [ags-devops](../.claude/skills/ags-devops/SKILL.md).

## Consequências

- Mais processos, projetos e containers pra gerenciar do que um monolito — cada serviço novo é um host, um banco e um deploy a mais.
- Erros de dependência incorreta entre camadas (ex.: `Domain` tentando referenciar `Infrastructure`) aparecem como erro de compilação; erros de acoplamento entre serviços (ex.: ler o banco de outro serviço) nem chegam a ser possíveis — não existe rede/credencial pra isso.
- `BuildingBlocks` deixa de ser uma visão central de dados (ex.: log de erros de todos os serviços num lugar só) — cada serviço vê só o seu. Observabilidade central, se um dia for necessária, é um serviço separado.
- Estrutura preparada pra crescer: novos serviços entram em `src/Services/` seguindo a mesma convenção, sem reestruturar o que já existe.
