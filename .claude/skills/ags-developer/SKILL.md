---
name: ags-developer
description: Convenções de desenvolvimento C#/.NET do projeto Ouroboros — branch de trabalho, regras de commit/push, idioma, arquitetura limpa e padrões de nomenclatura/formatação de código. Use sempre que for escrever, revisar, refatorar ou sugerir código C# neste projeto.
---

# ags-developer

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
- Ver [src/Services/README.md](../../../src/Services/README.md) para a convenção de estrutura e a regra de isolamento entre serviços.

## Nomenclatura (casing)

- **camelCase**: variáveis locais e campos privados (ex.: `_orderStatus`, `totalAmount`).
- **PascalCase**: classes, métodos, propriedades públicas e demais membros públicos — padrão idiomático do C#/.NET (ex.: `OrderStatus`, `CalculateTotal()`). Não usar camelCase em propriedades públicas.

## Nomenclatura de métodos (verbo explícito)

- Todo método que executa uma ação deve ter um verbo explícito indicando o que ele faz (ex.: `Add`, `Get`, `Update`, `Delete`, `Create`, `Remove`), antes ou depois do nome do recurso — nunca um nome vago que exija ler o corpo do método pra saber o que ele faz.
- Exemplo aplicado: `IErrorLogService.AddAsync(...)` (adiciona um registro de erro), não `LogAsync(...)` (não deixa claro se loga, cria, envia, etc.).

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

## Testes

- Todo serviço/caso de uso ou regra de negócio novo deve ser coberto por um teste correspondente — para as regras de cobertura e demais convenções de teste, siga a skill [ags-qa](../ags-qa/SKILL.md).
- `dotnet build` já executa os testes automaticamente ao final (ver `Directory.Build.targets` na raiz do repositório) — não é preciso rodar `dotnet test` manualmente à parte, embora nada impeça. Essa automação só dispara ao buildar a `Ouroboros.Services.Auth.Api` (projeto de entrada); buildar um projeto individual isoladamente não aciona os testes.

## Banco de dados

- Qualquer decisão ou implementação envolvendo banco de dados (schemas, migrations, nomenclatura de tabelas/colunas, etc.) segue a skill [ags-dba](../ags-dba/SKILL.md).

## Tratamento de erros

- Não usar `try/catch` só pra logar e relançar (ou engolir) uma exceção — deixe subir. Qualquer erro não tratado que chegue até a Api de um serviço é capturado automaticamente pelo `GlobalExceptionHandler` daquele serviço (ex.: `src/Services/Auth/Ouroboros.Services.Auth.Api/GlobalExceptionHandler.cs`) e registrado via `IErrorLogService`, sem precisar de código extra em cada método.
- Só usar `try/catch` quando houver algo real a fazer com a exceção naquele ponto (recuperar, traduzir para um erro de domínio específico, tentar de novo, etc.) — nunca apenas para logar.
- Mecanismo completo documentado em [docs/0000 - Arquitetura.md](../../../docs/0000%20-%20Arquitetura.md).

## Autorização de endpoints

- Toda Api de serviço exige autenticação (JWT Bearer) por padrão — não por convenção lembrada a cada vez, mas por uma `FallbackPolicy` configurada em `Program.cs` (`RequireAuthenticatedUser()`), que se aplica a qualquer endpoint sem anotação explícita. Endpoints públicos precisam ser marcados com `[AllowAnonymous]`, não o contrário.
- Exceções conhecidas hoje (ficam `[AllowAnonymous]`): `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/confirm-email`, `GET /api/auth/confirm-email`, `POST /api/auth/refresh-token`, `POST /api/auth/forgot-password` e `POST /api/auth/reset-password` — são os endpoints que o usuário usa antes de ter um `AccessToken` válido (ou, no caso do `refresh-token`, justamente porque o `AccessToken` já expirou). `POST /api/auth/logout` é a exceção contrária: mesmo sendo parte do fluxo de autenticação, exige `AccessToken` válido, porque só faz sentido chamado por quem já está autenticado.
- Ao criar um endpoint novo, se não estiver claro se ele deve ser público ou exigir autenticação, **pergunte ao usuário antes de decidir** — não presuma nem `[Authorize]` nem `[AllowAnonymous]` por conta própria fora da lista de exceções conhecidas acima.

## Collection do Postman

- Sempre que um método/endpoint novo for criado ou alterado numa Api de serviço, revisar e ajustar a collection Postman daquele serviço (ex.: `src/Services/Auth/Ouroboros.Services.Auth.Api/Postman/Ouroboros.postman_collection.json`) para refletir a mudança (nova requisição, parâmetros, exemplos, etc.). O `baseUrl` da collection aponta pro Api Gateway, não pra porta interna do serviço.

## Documentação

Segue a skill [ags-technical-writer](../ags-technical-writer/SKILL.md).
