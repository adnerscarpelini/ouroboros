# Evidências

## Execução

- Comando: `dotnet test Ouroboros.slnx`
- Data: 06/10/2026
- Resultado: 387 testes, 387 aprovados, 0 falhas. Saída bruta em `dotnet-test.txt`.

| Projeto | Total | Passou | Falhou |
|---|---|---|---|
| Auth.Domain.Tests | 61 | 61 | 0 |
| Auth.Application.Tests | 165 | 165 | 0 |
| Auth.Integration.Tests | 161 | 161 | 0 |

Antes desta spec eram 357 testes (56 + 154 + 147). Os testes de integração usam um SQL Server real em container (Testcontainers).

## Rastreabilidade

Caminhos relativos a `test/auth-service/`.

### Decisões da spec

| Decisão | Teste | Resultado |
|---|---|---|
| 1. Várias sessões simultâneas | `Auth.Application.Tests/UseCases/Login/LoginInteractorTests.cs::ShouldKeepPreviousSessionsActiveWhenUserLogsInAgain`; `Auth.Integration.Tests/Api/SessionApiTests.cs::ShouldKeepBothSessionsValidWhenUserLogsInTwice` | Passou |
| 2. Sessão = família de refresh tokens (`session_id`) | `Auth.Domain.Tests/Entities/RefreshTokenTests.cs::ShouldStartANewSessionWhenNoSessionIsGiven`, `::ShouldKeepTheGivenSessionWhenCreatingTheSuccessor`, `::ShouldKeepTheSessionWhenRehydrating`; `Auth.Integration.Tests/Api/MultiSessionApiTests.cs::ShouldCreateOneSessionPerLoginAndPutItsIdInTheSidClaim`, `::ShouldKeepTheSessionIdAcrossRefreshRotation`; `Auth.Application.Tests/UseCases/RefreshAccessToken/RefreshAccessTokenInteractorTests.cs::ShouldInheritSessionIdInTheSuccessorTokenWhenRefreshTokenIsRotated` | Passou |
| 3. Claim `sid` no access token | `LoginInteractorTests::ShouldIssueAccessTokenWithTheSessionIdOfTheNewRefreshToken`; `RefreshAccessTokenInteractorTests::ShouldKeepSessionIdClaimInTheNewAccessTokenWhenRefreshTokenIsRotated`; `MultiSessionApiTests::ShouldCreateOneSessionPerLoginAndPutItsIdInTheSidClaim` (lê o claim do JWT real) | Passou |
| 4. Limite de sessões descartado | só documentado, sem mudança de código | Não se aplica |
| 5. `POST /api/auth/logout-all` (`[Authorize]`, `204`) e logout só da sessão do token | `Auth.Application.Tests/UseCases/LogoutAll/LogoutAllInteractorTests.cs::ShouldRevokeEverySessionOfTheUserWhenLoggingOutOfAll`, `::ShouldNotTouchSessionsOfOtherUsersWhenLoggingOutOfAll`, `::ShouldCompleteWhenUserHasNoActiveSession`; `MultiSessionApiTests::ShouldEndEverySessionOfTheUserOnLogoutAllAndKeepOtherUsersSessions`, `::ShouldReturnUnauthorizedOnLogoutAllWithoutAccessToken`, `::ShouldAcceptLogoutAllTwiceBecauseItIsIdempotent`, `::ShouldKeepAccessTokenAlreadyIssuedValidAfterLogoutAll`, `::ShouldEndOnlyTheSessionOfTheLoggedOutRefreshToken` | Passou |
| 6. Login atômico (refresh token, `last_login_at` e contador na mesma transação; tokens só depois do commit) | `LoginInteractorTests::ShouldRunAllWritesInsideTheSameUnitOfWork`, `::ShouldRollBackAndCreateNoRefreshTokenWhenAccountIsLockedBetweenReadAndWrite`, `::ShouldNotOpenUnitOfWorkWhenUserIsInactive`; `MultiSessionApiTests::ShouldCreateNoRefreshTokenAndKeepUserUntouchedWhenUserUpdateFails` (falha antes e depois do comando), `::ShouldRollBackLastLoginAtWhenRefreshTokenInsertFails` | Passou |
| 7. `LastLoginAt` gravado, fora das respostas | `Auth.Domain.Tests/Entities/UserTests.cs::ShouldRecordLastLoginAndClearFailuresWhenLoginIsRegistered`, `::ShouldHaveNoLastLoginWhenUserIsCreated`; `LoginInteractorTests::ShouldRecordLastLoginAtWhenLoginSucceeds`, `::ShouldNotRecordLastLoginAtWhenLoginIsRejected`; `MultiSessionApiTests::ShouldRecordLastLoginAtAndResetFailureCountWhenLoginSucceeds`, `::ShouldNotRecordLastLoginAtWhenLoginIsRejected`, `::ShouldNotExposeLastLoginAtOrSessionIdInTheLoginBody` | Passou |
| 8. Migration de `session_id` com backfill, `NOT NULL` e índice `(user_id, session_id)` | `Auth.Integration.Tests/Persistence/SessionIdMigrationTests.cs::ShouldGiveEachExistingRefreshTokenItsOwnSessionAndMakeTheColumnRequired` (banco no estado anterior, 3 tokens de 2 usuários) | Passou |

### Tarefa Dev

| Critério | Teste | Resultado |
|---|---|---|
| Login sem revogar sessões anteriores, `SessionId` em `RefreshToken`, claim `sid` | `LoginInteractorTests`, `RefreshTokenTests`, `MultiSessionApiTests` | Passou |
| `User.RegisterLogin` e login dentro do `IUnitOfWork`, tokens só depois do commit | `UserTests`, `LoginInteractorTests`, `MultiSessionApiTests` | Passou |
| Caso de uso `LogoutAll`, endpoint e collection Postman | `LogoutAllInteractorTests`, `MultiSessionApiTests`; request "Logout All" em `auth-service/Auth.Api/Postman/Auth.postman_collection.json` | Passou |

### Tarefa DBA — migration e persistência

| Critério | Teste | Resultado |
|---|---|---|
| Migration com backfill, `NOT NULL` e índice | `SessionIdMigrationTests` | Passou |
| `session_id` gravado e lido no `DapperRefreshTokenRepository` | `MultiSessionApiTests::ShouldCreateOneSessionPerLoginAndPutItsIdInTheSidClaim`, `::ShouldKeepTheSessionIdAcrossRefreshRotation` | Passou |

### Tarefa Tester

| Critério | Teste | Resultado |
|---|---|---|
| Unitários: login não revoga sessões, `LastLoginAt`, contador zerado, token com `sid`, logout-all | `LoginInteractorTests`, `LogoutAllInteractorTests`, `UserTests`; contador zerado também em `MultiSessionApiTests::ShouldRecordLastLoginAtAndResetFailureCountWhenLoginSucceeds` | Passou |
| Integração: dois logins → duas sessões válidas; falha forçada na atualização do usuário → nenhum refresh token; logout de uma sessão não afeta a outra | `SessionApiTests::ShouldKeepBothSessionsValidWhenUserLogsInTwice`; `MultiSessionApiTests::ShouldCreateNoRefreshTokenAndKeepUserUntouchedWhenUserUpdateFails`, `::ShouldEndOnlyTheSessionOfTheLoggedOutRefreshToken` | Passou |

### Tarefa Tech Writer

| Critério | Artefato | Resultado |
|---|---|---|
| "Uma sessão ativa" trocada por várias sessões, `sid`, logout-all e garantia transacional | `docs/auth/0003 - Login e Tokens.md` (seções "Login", "Sessões", "Logout", "Revogação", "Access token" e "Refresh token") | Entregue |

## Observações

- **`TryRegisterLoginAsync`.** O gateway ganhou `IUserRepository.TryRegisterLoginAsync`, um `UPDATE` único que grava `last_login_at`, zera `access_failed_count` e `lockout_end` e só age se a conta não estiver bloqueada nem excluída. O `UpdateAsync` não serve: ele não grava o contador (de propósito, para não sobrescrever o contador atômico). Os nove repositórios falsos dos testes de Application ganharam o método. O `TryResetFailedAccessAsync` continua, porque a exclusão de conta o usa.
- **Verificação de usuário inativo.** Passou para antes da transação: um usuário inativo com a senha certa não abre `IUnitOfWork` e não tem o contador zerado.
- **Logout.** O `POST /api/auth/logout` já encerrava só o token enviado. Como a rotação revoga o token anterior, há no máximo um token ativo por sessão, então isso equivale a encerrar a sessão. Não houve mudança de código, e o teste de integração prova que a outra sessão não é afetada. A revogação por `session_id` (`RevokeAllActiveBySessionAsync`) fica para a spec 2026092507, que a usa na detecção de reuso.
- **Re-hash da spec 2026092509** passou a rodar dentro da mesma transação do login.
- **Teste da migration.** Cria um banco descartável no mesmo container, aplica todas as migrations menos a do `session_id`, insere tokens e roda o `MigrationRunner`.
