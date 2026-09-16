# 0005 - Repositórios e Unidade de Trabalho

## Contexto

Os casos de uso do Auth (registrar usuário, confirmar e-mail, login, refresh, logout, redefinir senha) usam contratos da `Application` e implementações SQL na `Infrastructure`.

Isso invertia o objetivo da Clean Architecture na prática:

- A `Application`, que deveria ser a camada independente de framework, só tinha interfaces e DTOs — nenhuma regra.
- A regra de negócio ficava soldada à infraestrutura, na camada mais externa das três.
- O sintoma: `Ouroboros.Services.Auth.Application.Tests` não tinha nenhum teste (não havia o que testar ali), e toda a regra era testada em `Infrastructure.Tests`, sempre acoplada à infraestrutura.

Havia também um problema de consistência: cada caso de uso chamava `SaveChanges` várias vezes, sem transação. Uma falha no meio de `CreateUserAsync` deixava um usuário gravado sem nenhum token de confirmação — ou seja, uma conta impossível de ativar.

## Decisão

Os casos de uso moram na `Application`. A `Application` fala com o banco apenas por contratos que ela mesma declara; a `Infrastructure` implementa esses contratos com SQL explícito via Npgsql.

### Repositórios

- Um contrato por agregado, em `Application/Interfaces/`: `IUserRepository`, `ITokenRepository`, `IRefreshTokenRepository`, `ITokenTypeRepository`.
- Implementação em `Infrastructure/Persistence/Repositories/`, usando `DbSession` e SQL parametrizado.
- Os métodos são específicos da intenção (`GetByHashAsync`, `ExistsByLoginAsync`), não genéricos (`GetAll`, `Find`). Um repositório genérico esconderia a intenção da consulta e dificultaria a revisão do SQL.
- `Add` só marca a entidade para inclusão. Quem grava é o `IUnitOfWork`.

### Unidade de trabalho

- `IUnitOfWork` (em `Application/Interfaces/`) expõe `SaveChangesAsync` e `ExecuteInTransactionAsync`.
- A implementação (`Infrastructure/Persistence/UnitOfWork.cs`) coordena a mesma `DbSession` que os repositórios usam — por isso tudo cai na mesma transação.
- `ExecuteInTransactionAsync` mantém a transação explícita e os comandos SQL pendentes no mesmo contexto de execução.
- Um caso de uso que precise de mais de um `SaveChanges` usa `ExecuteInTransactionAsync`. É o caso de `CreateUserAsync`, que precisa gravar a mensagem de e-mail antes de criar o `Token` que aponta pra ela (o id da mensagem só existe depois de gravada).

### Referências navegáveis no Domain

`Token` e `RefreshToken` passaram a referenciar `TokenType` e `User` como objetos, não como ids:

```csharp
new Token(tokenType: tokenType, user: user, emailMessageId: ..., tokenHash: ..., expiresAt: ...)
```

- O caso de uso deixa de depender de um id que só existe depois de gravar — o que é o que permite testá-lo sem banco nenhum.
- As colunas e chaves estrangeiras são exatamente as mesmas de antes (`token_type_id`, `user_id`). A migration correspondente é registrada como SQL versionado; alterações de modelo que não mudam o banco não geram migration.
- Quem lê do banco precisa trazer os dados relacionados explicitamente no `SELECT` — por isso `GetByHashAsync` carrega `TokenType` e `User`.
- `Token.NotificationRequestId` é um `Guid` solto de propósito: a entrega vive no banco do serviço de Notificações, e chave estrangeira não atravessa serviço. O token também sobrevive à limpeza da outbox, então nem uma FK local caberia — ver [0007](0007%20-%20Fila%20de%20E-mails%20%28Outbox%29.md).

### Um serviço por assunto

`IUserService` tinha 7 métodos e acumulava três assuntos diferentes. Foi dividido em `IUserRegistrationService` (registro e confirmação), `IAuthenticationService` (login, refresh, logout) e `IPasswordResetService` (esqueci/redefinir senha).

## O que isso comprou

- `Auth.Application.Tests` testa toda a regra de negócio com fakes em memória, sem banco — ver `AuthTestContext`.
- `Auth.Infrastructure.Tests` passa a testar o que só a infraestrutura sabe: se os repositórios trazem as navegações certas e se as consultas filtram o que deveriam.
- A atomicidade é verificável: um teste afirma que `CreateUserAsync` roda tudo numa transação só.

## Consequências

- Mais arquivos por caso de uso: um contrato na `Application` e uma implementação na `Infrastructure`, em vez de uma sessão SQL injetada diretamente no caso de uso.
- Repositórios usam SQL explícito e mapeamento manual; não há ORM ou micro-ORM na persistência.
- Consultas pesadas de leitura continuam seguindo o padrão de Query Object com SQL e Npgsql descrito em [0004 - SQL explícito e persistência](0004%20-%20SQL%20explícito%20e%20persistência.md).
- `ExecuteInTransactionAsync` deve ser exercitado em testes de integração contra o Postgres real.
