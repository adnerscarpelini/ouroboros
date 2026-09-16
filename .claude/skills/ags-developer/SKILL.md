---
name: ags-developer
description: Convenções de desenvolvimento C#/.NET do projeto Ouroboros — branch de trabalho, regras de commit/push, idioma, arquitetura limpa e padrões de nomenclatura/formatação de código. Use sempre que for escrever, revisar, refatorar ou sugerir código C# neste projeto.
---

# ags-developer

## Specs como histórias de trabalho

- Antes de desenvolver uma nova feature, procure em `specs/` uma spec correspondente e pergunte ao usuário se ela já existe.
- Se não existir, crie uma spec antes de implementar, usando `AAAAMMDDHHMMSS-Descricao.md` e metadados YAML `title` e `state: new`. A spec representa uma história/item de trabalho, no estilo Jira. Specs históricas mantêm seus nomes originais.
- Se existir, trabalhe a partir dela e atualize-a sempre que o escopo, decisões, arquivos afetados, critérios de aceite ou estado mudarem. Estados permitidos: `new`, `in progress` e `done`.
- Ao sugerir uma mensagem de commit, inclua o número/data da spec correspondente (por exemplo, `spec 2026-09-09`) junto do Conventional Commit.

Skill base para atuar como desenvolvedor no projeto Ouroboros. Segue estas regras ao trabalhar com código C#/.NET neste repositório.

## Controle de versão

- A branch de trabalho padrão é `development`. Todo desenvolvimento novo acontece nela, nunca diretamente na `main`.
- Antes de começar a trabalhar, confirme que está na `development` (ou numa branch derivada dela). Se estiver na `main`, troque antes de editar código.
- **Nunca faça commit ou push automaticamente.** Edite/crie os arquivos normalmente, mas deixe o commit e o push sempre a cargo do usuário — apenas avise que as mudanças estão prontas para revisão.
- Sempre que uma tarefa for validada e finalizada (build ok, testes ok), sugerir uma mensagem de commit pronta pra o usuário rodar, seguindo o padrão [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`, etc.).

## Idioma

- Nomes de classes, métodos, propriedades/atributos, variáveis, tabelas, colunas e demais identificadores de código: sempre em **inglês**.
- Comentários e documentação (XML docs, README, markdown, etc.): sempre em **português do Brasil**.
- Comentários só quando o "porquê" não é óbvio pelo código. Evitar comentário óbvio explicando o que o código já diz por si.

## Arquitetura e código limpo

- Priorizar Clean Architecture e Clean Code: separação clara de camadas (Domain, Application, Infrastructure, API/Presentation), regras de negócio isoladas do framework.
- Escrever só o necessário para o problema atual — evitar abstrações prematuras, generalizações especulativas, código morto ou duplicação desnecessária (KISS/YAGNI).
- Preferir nomes descritivos que dispensem comentário explicativo.

## Novo serviço vs. serviço existente

- Antes de implementar uma funcionalidade nova, analisar se ela pertence a um serviço já existente em `src/Services/` (mesmo contexto de negócio) ou se representa um contexto novo, que pede um serviço novo.
- Apresentar essa análise ao usuário antes de criar um serviço novo: qual serviço existente poderia acomodar a funcionalidade (se algum) e por quê, ou a sugestão de nome/escopo para o serviço novo. Só criar o serviço novo depois da confirmação do usuário — não decidir isso sozinho. Criar um serviço novo é uma decisão maior que criar um módulo: implica processo, banco e deploy próprios.
- Se a funcionalidade claramente pertence a um serviço já existente, pode seguir direto nele, sem precisar dessa confirmação.
- Confirmado o serviço novo, siga também a [ags-devops](../ags-devops/SKILL.md): a infraestrutura dele (container, porta, health check, rota no gateway) faz parte da mesma entrega.
- Ver [src/Services/README.md](../../../src/Services/README.md) para a convenção de estrutura e a regra de isolamento entre serviços.

## Nomenclatura (casing)

- **camelCase**: variáveis locais e campos privados (ex.: `_orderStatus`, `totalAmount`).
- **PascalCase**: classes, métodos, propriedades públicas e demais membros públicos — padrão idiomático do C#/.NET (ex.: `OrderStatus`, `CalculateTotal()`). Não usar camelCase em propriedades públicas.

## Nomenclatura de métodos (verbo explícito)

- Todo método que executa uma ação deve ter um verbo explícito indicando o que ele faz (ex.: `Add`, `Get`, `Update`, `Delete`, `Create`, `Remove`), antes ou depois do nome do recurso — nunca um nome vago que exija ler o corpo do método pra saber o que ele faz.
- Exemplo aplicado: `IErrorLogService.AddAsync(...)` (adiciona um registro de erro), não `LogAsync(...)` (não deixa claro se loga, cria, envia, etc.).

## Nomenclatura de serviços e componentes

- Classes da camada `Application/UseCases` representam uma operação exposta à Api — uma por caso de uso — e terminam com `UseCase`, como `RegisterUserUseCase`, `LoginUseCase`, `ConfirmEmailUseCase` e `RequestPasswordResetUseCase`. A interface correspondente mora em `Application/Interfaces` com o mesmo conceito no nome: `ILoginUseCase`/`LoginUseCase`.
- Um serviço com múltiplas operações relacionadas (ex.: login, refresh token e logout) vira uma classe `UseCase` por operação, não uma classe só com vários métodos — cada caso de uso é testável e injetável isoladamente. Lógica realmente compartilhada entre duas ou mais `UseCase`s (ex.: emitir o par access+refresh token) vai para um colaborador interno em `Application/Services`, que não implementa nenhuma interface exposta à Api.
- Classes concretas que representam serviços executáveis — tanto esses colaboradores internos da `Application` quanto qualquer implementação técnica na `Infrastructure` — terminam com `Service`. Isso inclui implementações de `Renderer`, `Publisher`, `Processor`, `Sender`, `Accessor`, `Generator`, `Provider` e `Queue`.
- O nome deve preservar a responsabilidade antes do sufixo: `EmailTemplateRendererService`, `EmailDeliveryProcessorService`, `SmtpEmailSenderService` e `OutboxPublisherService`.
- Componentes que não são serviços executáveis, como entidades, records de dados, módulos estáticos, mappers e factories, mantêm o sufixo específico da sua responsabilidade (`Entity`, `Options`, `Module`, `Mapper`, `Factory`, etc.) — ex.: `AuthenticationResultFactory`, o colaborador citado acima.
- Ao revisar uma padronização, verificar todas as classes da camada correspondente, seus arquivos, referências, registros de DI, construtores, testes e configurações.

## Nomenclatura de repositórios

- Implementações de persistência devem usar o nome do agregado/entidade seguido de `Repository`: `UserRepository`, `TokenRepository`, `RefreshTokenRepository` e `TokenTypeRepository`.
- Não adicionar prefixos tecnológicos como `Sql` ou nomes de frameworks ao nome da implementação. A tecnologia é detalhe da infraestrutura e não faz parte do contrato nominal do repositório.
- O arquivo deve ter o mesmo nome da classe concreta e ficar em `Infrastructure/Persistence/Repositories/`.
- Mesmo que o repositório tenha apenas um método, ele mantém o sufixo `Repository` para preservar um padrão único.

## Formatação de assinaturas de métodos

- Método/construtor com **0 ou 1 parâmetro**: assinatura em uma única linha.
- Método/construtor com **2 ou mais parâmetros**: quebrar um parâmetro por linha.

```csharp
// 1 parâmetro: uma linha
public void SetStatus(OrderStatus status) { ... }

// 2+ parâmetros: um por linha
public void Insert(
	string userId,
	string fullName,
	string userLogin,
	string passwordHash,
	int userStatusId,
	int userProfileId,
	int? personId
)
{
	...
}
```

- Nas chamadas de métodos com 2 ou mais parâmetros, sempre usar **named arguments**, um por linha, para facilitar a leitura:

```csharp
userService.Insert(
	userId: userId,
	fullName: fullName,
	userLogin: userLogin,
	passwordHash: passwordHash,
	userStatusId: userStatusId,
	userProfileId: userProfileId,
	personId: personId
);
```

## Legibilidade e espaçamento do código

- Uma instrução C# deve ocupar sua própria linha. Nunca juntar comandos independentes com `;` na mesma linha.
- Separar visualmente as etapas lógicas de um método com uma linha em branco: preparar dados, criar o comando, adicionar parâmetros, executar, ler o resultado, mapear e retornar.
- `if`, `else`, `foreach`, `while`, `for`, `try`, `catch` e `finally` devem sempre usar chaves, mesmo quando o bloco tiver uma única instrução.
- Nunca colocar `return`, `throw` ou outra instrução na mesma linha da condição. Usar sempre um bloco explícito:

```csharp
if (condition)
{
	return result;
}
```

- Chamadas com muitos argumentos, construções de objetos e expressões de mapeamento devem ser quebradas em múltiplas linhas, com um argumento por linha quando isso melhorar a leitura.
- Em código de persistência, manter separadas as etapas de comando SQL, parâmetros, execução, leitura do `reader` e mapeamento.
- A prioridade de formatação é a leitura humana: o código deve permitir identificar rapidamente cada etapa sem depender de leitura horizontal extensa.

## Testes

- Todo serviço/caso de uso ou regra de negócio novo deve ser coberto por um teste correspondente — para as regras de cobertura e demais convenções de teste, siga a skill [ags-qa](../ags-qa/SKILL.md).
- `dotnet build` já executa os testes automaticamente ao final (ver `Directory.Build.targets` na raiz do repositório) — não é preciso rodar `dotnet test` manualmente à parte, embora nada impeça. Essa automação só dispara ao buildar a `Ouroboros.AuthService.Api` (projeto de entrada); buildar um projeto individual isoladamente não aciona os testes.

## Banco de dados

- Qualquer decisão ou implementação envolvendo banco de dados (schemas, migrations, nomenclatura de tabelas/colunas, etc.) segue a skill [ags-dba](../ags-dba/SKILL.md).

## Infraestrutura, container e deploy

- Qualquer coisa que envolva **rodar** o projeto em vez de escrevê-lo — `Dockerfile`, `docker-compose.yml`, portas, health checks, variáveis de ambiente, segredos, rota ou rate limiting no Api Gateway — segue a skill [ags-devops](../ags-devops/SKILL.md).
- **Toda Api nova nasce containerizada.** Criar um serviço não é só criar os projetos: o `Dockerfile`, o serviço no Compose, os health checks e a rota no gateway entram na mesma tarefa. O checklist completo está na `ags-devops`.

## Tratamento de erros

- Não usar `try/catch` só pra logar e relançar (ou engolir) uma exceção — deixe subir. Qualquer erro não tratado que chegue até a Api de um serviço é capturado automaticamente pelo `GlobalExceptionHandler` daquele serviço (ex.: `src/Services/AuthService/Ouroboros.AuthService.Api/GlobalExceptionHandler.cs`) e registrado via `IErrorLogService`, sem precisar de código extra em cada método.
- Só usar `try/catch` quando houver algo real a fazer com a exceção naquele ponto (recuperar, traduzir para um erro de domínio específico, tentar de novo, etc.) — nunca apenas para logar.
- Mecanismo completo documentado em [docs/0000 - Arquitetura.md](../../../docs/0000%20-%20Arquitetura.md).

## Autorização de endpoints

- Toda Api de serviço exige autenticação (JWT Bearer) por padrão — não por convenção lembrada a cada vez, mas por uma `FallbackPolicy` configurada em `Program.cs` (`RequireAuthenticatedUser()`), que se aplica a qualquer endpoint sem anotação explícita. Endpoints públicos precisam ser marcados com `[AllowAnonymous]`, não o contrário.
- Exceções conhecidas hoje (ficam `[AllowAnonymous]`): `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/confirm-email`, `GET /api/auth/confirm-email`, `POST /api/auth/refresh-token`, `POST /api/auth/forgot-password` e `POST /api/auth/reset-password` — são os endpoints que o usuário usa antes de ter um `AccessToken` válido (ou, no caso do `refresh-token`, justamente porque o `AccessToken` já expirou). `POST /api/auth/logout` é a exceção contrária: mesmo sendo parte do fluxo de autenticação, exige `AccessToken` válido, porque só faz sentido chamado por quem já está autenticado.
- Ao criar um endpoint novo, se não estiver claro se ele deve ser público ou exigir autenticação, **pergunte ao usuário antes de decidir** — não presuma nem `[Authorize]` nem `[AllowAnonymous]` por conta própria fora da lista de exceções conhecidas acima.

## Collection do Postman

- Sempre que um método/endpoint novo for criado ou alterado numa Api de serviço, revisar e ajustar a collection Postman daquele serviço (ex.: `src/Services/AuthService/Ouroboros.AuthService.Api/Postman/Ouroboros.postman_collection.json`) para refletir a mudança (nova requisição, parâmetros, exemplos, etc.). O `baseUrl` da collection aponta pro Api Gateway, não pra porta interna do serviço.

## Documentação

Segue a skill [ags-technical-writer](../ags-technical-writer/SKILL.md).
