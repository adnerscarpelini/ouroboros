# Evidências

## Execução

- Comando: `dotnet test Ouroboros.slnx`
- Data: 06/10/2026
- Resultado: 287 testes, 287 aprovados, 0 falhas. Saída bruta em `dotnet-test.txt`.

| Projeto | Total | Passou | Falhou |
|---|---|---|---|
| Auth.Domain.Tests | 50 | 50 | 0 |
| Auth.Application.Tests | 130 | 130 | 0 |
| Auth.Integration.Tests | 107 | 107 | 0 |

A suíte de integração usa um SQL Server real em container (Testcontainers), com Docker 29.7.2. Os números são os da solution inteira no momento do arquivamento, e não só desta spec: a implementação da spec foi entregue antes (commit `b74e33f`) e esta execução confirma que ela continua valendo com as specs seguintes em cima.

## Rastreabilidade

Caminhos relativos a `test/auth-service/`.

### Decisões da spec

| Decisão | Teste | Resultado |
|---|---|---|
| 1. `IUnitOfWork` com commit e rollback em exceção | `Auth.Integration.Tests/Infrastructure/UnitOfWorkTests.cs::ShouldPersistBothWritesWhenWorkCompletes`, `::ShouldRollBackBothWritesWhenWorkThrows`, `::ShouldAllowAnotherUnitOfWorkAfterRollback` | Passou |
| 2. Fábrica de conexões, `DbSession` e comandos fora da unidade em autocommit | `UnitOfWorkTests::ShouldAutocommitWhenOutsideUnitOfWork`; todos os testes de integração passam por `DbSession` | Passou |
| 3. Sem transação aninhada | `UnitOfWorkTests::ShouldThrowWhenUnitOfWorkIsNested` | Passou |
| 4. Isolamento Read Committed com lock ou escrita condicional por spec | cobertos por spec: `RepositorySqlTests::ShouldRevokeRefreshTokenInExactlyOneOfManyConcurrentCalls` e `::ShouldMarkTokenAsUsedInExactlyOneOfManyConcurrentCalls` (escritas condicionais); o lock explícito fica na spec 2026092505 | Passou |
| 5. Nada sensível sai antes do commit | `Auth.Integration.Tests/Api/RegistrationApiTests.cs::ShouldRollBackUserWhenFailureHappensAfterTokenInsert` (o token só é devolvido depois do commit) | Passou |
| 6. Escritas que sobrevivem ao rollback ficam fora da transação | contador de falhas: `Auth.Integration.Tests/Auth/AuthenticationProtectionTests.cs`; reuso de refresh token e auditoria ficam nas specs 2026092507 e 2026092519 | Passou (parcial: só o contador de falhas existe hoje) |
| 7. `SqlClient` fora de Domain e Application | `Auth.Domain` e `Auth.Application` não referenciam `Microsoft.Data.SqlClient` (build da solution) | Passou |

### Tarefas

| Tarefa | Evidência | Resultado |
|---|---|---|
| Dev — `IUnitOfWork` em `Auth.Application/Gateways` e registro no `UseCaseConfiguration` | `UnitOfWorkTests` e `RegistrationApiTests` resolvem o `IUnitOfWork` pelo DI da API real | Passou |
| DBA — fábrica de conexões, `DbSession` e `SqlUnitOfWork` | `UnitOfWorkTests` (5 testes) | Passou |
| DBA — repositórios Dapper recebem `DbSession` | `Persistence/RepositorySqlTests.cs` (todos) e `Api/*ApiTests.cs` | Passou |
| Tester — `FakeUnitOfWork` à mão | `Auth.Application.Tests/Fakes/FakeUnitOfWork.cs`, usado por `RegisterUserInteractorTests`, `ConfirmEmailInteractorTests` e `ResetPasswordInteractorTests` | Passou |
| Tester — integração: commit, rollback, aninhada e autocommit | `UnitOfWorkTests` (5 testes) | Passou |
| Tech Writer — seção de transações em `docs/project/0001 - Arquitetura.md` | seção "Transações" (`IUnitOfWork`, `DbSession`, quando usar e o que fica fora da transação) | Entregue |

## Observações

- A tarefa do Tech Writer já estava escrita no documento, mas a caixa nunca tinha sido marcada. Foi conferida e marcada no arquivamento.
- Na decisão 6, só o contador de falhas de senha (spec 2026092501) existe hoje. A revogação por reuso (2026092507) e a auditoria de falhas (2026092519) ainda vão usar essa regra e provar o comportamento nas suas próprias specs.
