# Evidências

## Execução

- Comando: `dotnet test Ouroboros.slnx`
- Data: 06/10/2026
- Resultado: 259 testes, 259 aprovados, 0 falhas. Saída bruta em `dotnet-test.txt`.

| Projeto | Total | Passou | Falhou |
|---|---|---|---|
| Auth.Domain.Tests | 50 | 50 | 0 |
| Auth.Application.Tests | 116 | 116 | 0 |
| Auth.Integration.Tests | 93 | 93 | 0 |

A suíte de integração sobe um SQL Server real (`mcr.microsoft.com/mssql/server:2022-latest`) em container, via Testcontainers. Foi rodada com Docker 29.7.2.

Na primeira execução, 2 testes novos falharam por erro do próprio teste (a entidade recusava a segunda revogação antes de o SQL ser chamado). Foram corrigidos para ler todas as instâncias antes e disputar só a escrita. Nenhum código de produção mudou nesta spec.

## Rastreabilidade

Caminhos relativos a `test/auth-service/Auth.Integration.Tests/`.

### Decisões da spec

| Decisão | Teste | Resultado |
|---|---|---|
| 1. Projeto xUnit na solution | `Auth.Integration.Tests.csproj` e `Ouroboros.slnx` | Passou |
| 2. SQL Server real via Testcontainers, migrations pelo `MigrationRunner` | `Infrastructure/AuthApiFixture.cs` (usada por todos os testes) | Passou |
| 3. API em memória com `WebApplicationFactory<Program>` e limites ajustáveis | `Infrastructure/AuthApiFactory.cs`, `Auth/AuthenticationProtectionTests.cs::ShouldReturnTooManyRequestsWhenIpExceedsPolicyLimit` | Passou |
| 4. Connection string válida já no startup | `Infrastructure/AuthApiFixture.cs::InitializeAsync` (as migrations rodam no startup) | Passou |
| 5. Banco zerado entre testes, sem paralelismo na collection | `Infrastructure/AuthApiFixture.cs::ResetDatabaseAsync`, `AuthApiCollection` | Passou |
| 6. Decorator de falha controlada | `Infrastructure/FaultInjection.cs`; `Api/RegistrationApiTests.cs::ShouldRollBackUserWhenConfirmationTokenInsertFails` e `::ShouldRollBackUserWhenFailureHappensAfterTokenInsert`; `Auth/ConcurrentRegistrationTests.cs::ShouldKeepAbandonedAccountAndCreateNoUserWhenTokenInsertFails` | Passou |
| 7. Linha de base dos fluxos existentes | tabela abaixo | Passou |
| 8. Só Docker como requisito, sem segredos reais | chave e senhas de teste fixas em `AuthApiFactory.cs` e `TestApi.cs` | Passou |

### Tarefa Dev — `Program` parcial e configuração no startup

| Critério | Teste | Resultado |
|---|---|---|
| `public partial class Program` exposto | `WebApplicationFactory<Program>` compila e sobe | Passou |
| Configuração injetada vale nas migrations do startup | `AuthApiFixture.InitializeAsync` aplica as migrations no container | Passou |

### Tarefa Tester — linha de base pela API

| Fluxo | Teste (`Api/`, salvo indicação) | Resultado |
|---|---|---|
| Cadastro | `RegistrationApiTests::ShouldCreateInactiveUserAndConfirmationTokenWhenDataIsValid`, `::ShouldNotStorePasswordInPlainText`, `::ShouldReturnBadRequestWhenLoginIsAlreadyInUse`, `::ShouldReturnBadRequestWhenPasswordIsWeak`, `::ShouldReturnBadRequestWhenEmailIsInvalid` | Passou |
| Resposta genérica para e-mail ocupado | `RegistrationApiTests::ShouldReturnSameBodyWhenEmailIsAlreadyInUse` | Passou |
| Confirmação | `EmailConfirmationApiTests::ShouldActivateUserAndConsumeTokenWhenTokenIsValid`, `::ShouldAllowLoginOnlyAfterConfirmation`, `::ShouldRejectSecondConfirmationWithTheSameToken`, `::ShouldRejectUnknownToken`, `::ShouldRejectExpiredTokenAndKeepUserInactive`, `::ShouldRejectPasswordResetTokenAsConfirmationToken` | Passou |
| Login | `SessionApiTests::ShouldReturnBearerTokensAndStoreOnlyTheRefreshTokenHashWhenLoginSucceeds`, `::ShouldReturnUnauthorizedWithTheSameBodyForWrongPasswordAndUnknownLogin`, `::ShouldReturnBadRequestWhenUserIsNotActive`, `::ShouldEndPreviousSessionWhenUserLogsInAgain` | Passou |
| Refresh com rotação | `SessionApiTests::ShouldRotateRefreshTokenAndRejectTheOldOne`, `::ShouldReturnUnauthorizedWhenRefreshTokenIsEmptyOrUnknown`, `::ShouldReturnUnauthorizedWhenRefreshTokenIsExpired`, `::ShouldRejectRefreshWhenUserWasDeleted` | Passou |
| Logout | `SessionApiTests::ShouldRevokeRefreshTokenOnLogoutAndBeIdempotent`, `::ShouldReturnUnauthorizedOnLogoutWhenRefreshTokenIsEmptyOrUnknown` | Passou |
| Reset de senha | `PasswordResetApiTests` (8 testes: pedido, resposta genérica, troca com fim das sessões, reuso, expirado, tipo errado, senha rejeitada não consome o token, senha igual à atual) | Passou |
| Consulta e regras de perfil e dono | `UserQueryApiTests::ShouldReturnOnlyAllowedFieldsWhenUserQueriesItself`, `::ShouldFindSelfBySearchByLoginAndEmail`, `::ShouldReturnForbiddenWhenCommonUserQueriesAnotherUser`, `::ShouldReturnAnotherUserWhenRequesterIsAdmin`, `::ShouldReturnBadRequestWhenSearchHasMoreThanOneCriterion`, `::ShouldNotReturnDeletedUserToAdmin` | Passou |
| Exclusão | `AccountDeletionApiTests` (7 testes: própria conta, senha errada, `403` de usuário comum, admin, último admin, e-mail liberado e login reservado) | Passou |
| `401` sem token ou com token inválido (middleware JWT) | `UserQueryApiTests::ShouldReturnUnauthorizedWhenThereIsNoToken`, `::ShouldReturnUnauthorizedWhenTokenIsSignedWithAnotherKey`, `::ShouldReturnUnauthorizedWhenTokenIsExpired`, `::ShouldReturnUnauthorizedWhenTokenUsesAlgorithmNone`; `AccountDeletionApiTests::ShouldReturnUnauthorizedWhenThereIsNoToken` | Passou |
| `403` por perfil | `UserQueryApiTests::ShouldReturnForbiddenWhenCommonUserQueriesAnotherUser`, `AccountDeletionApiTests::ShouldReturnForbiddenWhenCommonUserDeletesAnotherUser` | Passou |
| `429` nas sete políticas | `Auth/AuthenticationProtectionTests::ShouldReturnTooManyRequestsWhenIpExceedsPolicyLimit` (login, refresh, confirmação, cadastro, pedido e confirmação de reset) e `::ShouldReturnTooManyRequestsWhenIpExceedsDeleteLimit` (exclusão, que exige token) | Passou |

### Tarefa Tester — SQL dos repositórios

Todos em `Persistence/RepositorySqlTests.cs`.

| Critério | Teste | Resultado |
|---|---|---|
| Filtros de `deleted_at` | `ShouldIgnoreDeletedUserInEveryLookup`, `ShouldReportDeletedLoginOnlyForDeletedUsers`, `ShouldCountOnlyActiveAndNotDeletedAdmins` | Passou |
| Índice parcial de e-mail | `ShouldAllowSameEmailAfterAccountIsDeleted`, `ShouldKeepLoginReservedAfterAccountIsDeleted` | Passou |
| `ON DELETE CASCADE` | `ShouldRemoveTokensAndRefreshTokensWhenAbandonedUserIsRemoved`, `ShouldNeverRemoveLogicallyDeletedUser` | Passou |
| `TryRevokeAsync` | `ShouldRevokeRefreshTokenOnlyOnce`, `ShouldRevokeRefreshTokenInExactlyOneOfManyConcurrentCalls` | Passou |
| `TryMarkAsUsedAsync` | `ShouldMarkTokenAsUsedOnlyOnce`, `ShouldMarkTokenAsUsedInExactlyOneOfManyConcurrentCalls` | Passou |
| Revogação e invalidação em lote | `ShouldRevokeOnlyActiveRefreshTokensOfTheUser`, `ShouldInvalidateOnlyPendingTokensOfTheTypeAndUser` | Passou |
| Token pendente | `ShouldFindPendingTokenOnlyWhenNotUsedNotExpiredAndOfTheType` | Passou |

### Tarefa Tech Writer

| Critério | Artefato | Resultado |
|---|---|---|
| Doc com pré-requisitos, comando e como escrever um teste | `docs/project/0004 - Testes de Integracao.md` | Entregue |

## Observações

- O teste de rollback no refresh de token não entrou como linha de base. Sem transação, o comportamento atual deixa o token antigo revogado e o novo sem nascer. A spec 2026092507 corrige isso e acrescenta o próprio teste.
- A confirmação de e-mail ainda não é atômica (spec 2026092503). Os testes dela cobrem só o comportamento externo atual.
