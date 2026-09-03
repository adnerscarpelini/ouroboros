# 0005 - Repositórios e Unidade de Trabalho

## Contexto

Os casos de uso do Auth (registrar usuário, confirmar e-mail, login, refresh, logout, redefinir senha) nasceram em `UserService`, dentro da camada `Infrastructure`, dependendo direto do `AuthDbContext`.

Isso invertia o objetivo da Clean Architecture na prática:

- A `Application`, que deveria ser a camada independente de framework, só tinha interfaces e DTOs — nenhuma regra.
- A regra de negócio ficava soldada ao EF Core, na camada mais externa das três.
- O sintoma: `Ouroboros.Services.Auth.Application.Tests` não tinha nenhum teste (não havia o que testar ali), e toda a regra era testada em `Infrastructure.Tests`, sempre com um `DbContext` em memória.

Havia também um problema de consistência: cada caso de uso chamava `SaveChanges` várias vezes, sem transação. Uma falha no meio de `CreateUserAsync` deixava um usuário gravado sem nenhum token de confirmação — ou seja, uma conta impossível de ativar.

## Decisão

Os casos de uso moram na `Application`. A `Application` fala com o banco apenas por contratos que ela mesma declara; a `Infrastructure` implementa esses contratos com EF Core.

### Repositórios

- Um contrato por agregado, em `Application/Interfaces/`: `IUserRepository`, `ITokenRepository`, `IRefreshTokenRepository`, `ITokenTypeRepository`.
- Implementação em `Infrastructure/Persistence/Repositories/`, usando o `DbContext` do serviço.
- Os métodos são específicos da intenção (`GetByHashAsync`, `ExistsByLoginAsync`), não genéricos (`GetAll`, `Find`). Um repositório genérico só devolveria o `IQueryable` do EF com outro nome, e o acoplamento voltaria pela porta dos fundos.
- `Add` só marca a entidade para inclusão. Quem grava é o `IUnitOfWork`.

### Unidade de trabalho

- `IUnitOfWork` (em `Application/Interfaces/`) expõe `SaveChangesAsync` e `ExecuteInTransactionAsync`.
- A implementação (`Infrastructure/Persistence/UnitOfWork.cs`) delega ao mesmo `DbContext` que os repositórios usam — por isso tudo cai na mesma transação.
- `ExecuteInTransactionAsync` roda a operação dentro da estratégia de execução do provider (`CreateExecutionStrategy`), não o contrário: é ela que sabe repetir a operação em falha transitória, e um retry precisa refazer a transação inteira.
- Um caso de uso que precise de mais de um `SaveChanges` usa `ExecuteInTransactionAsync`. É o caso de `CreateUserAsync`, que precisa gravar a mensagem de e-mail antes de criar o `Token` que aponta pra ela (o id da mensagem só existe depois de gravada).

### Referências navegáveis no Domain

`Token` e `RefreshToken` passaram a referenciar `TokenType` e `User` como objetos, não como ids:

```csharp
new Token(tokenType: tokenType, user: user, emailMessageId: ..., tokenHash: ..., expiresAt: ...)
```

- O caso de uso deixa de depender de um id que só existe depois de gravar — o que é o que permite testá-lo sem banco nenhum.
- As colunas e chaves estrangeiras são exatamente as mesmas de antes (`token_type_id`, `user_id`). A migration `AddTokenAndRefreshTokenNavigations` é intencionalmente vazia: muda o modelo do EF Core, não o banco.
- Quem lê do banco precisa trazer a navegação junto (`Include`) — por isso `GetByHashAsync` carrega `TokenType` e `User`.
- `Token.EmailMessageId` continua sendo um id solto: `email_messages` vive no schema `common` e essa coluna nunca teve chave estrangeira.

### Um serviço por assunto

`IUserService` tinha 7 métodos e acumulava três assuntos diferentes. Foi dividido em `IUserRegistrationService` (registro e confirmação), `IAuthenticationService` (login, refresh, logout) e `IPasswordResetService` (esqueci/redefinir senha).

## O que isso comprou

- `Auth.Application.Tests` testa toda a regra de negócio com fakes em memória, sem EF Core e sem banco — ver `AuthTestContext`.
- `Auth.Infrastructure.Tests` passa a testar o que só a infraestrutura sabe: se os repositórios trazem as navegações certas e se as consultas filtram o que deveriam.
- A atomicidade é verificável: um teste afirma que `CreateUserAsync` roda tudo numa transação só.

## Consequências

- Mais arquivos por caso de uso: um contrato na `Application` e uma implementação na `Infrastructure`, em vez de um `DbContext` injetado direto.
- Repositório sobre EF Core é uma crítica conhecida (o `DbSet` já é um repositório). O que se ganha aqui não é trocar de ORM um dia — é manter a regra de negócio testável e livre de framework, que é o objetivo de estudo do projeto.
- Consultas pesadas de leitura não passam por repositório: continuam seguindo o padrão de Query Object com Dapper descrito em [0004 - EF Core e Dapper](0004%20-%20EF%20Core%20e%20Dapper.md).
- `ExecuteInTransactionAsync` não é exercitado por teste automatizado: o provider em memória usado nos testes não suporta transação. A garantia vem do teste manual contra o Postgres real.
