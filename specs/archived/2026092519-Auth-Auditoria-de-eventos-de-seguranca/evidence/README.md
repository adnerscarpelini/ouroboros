# Evidências

## Execução

- Comando: `dotnet test Ouroboros.slnx`
- Data: 06/10/2026
- Resultado: 579 testes, 579 aprovados, 0 falhas. Saída bruta em `dotnet-test.txt`.

| Projeto | Total | Passou | Falhou |
|---|---|---|---|
| Auth.Domain.Tests | 61 | 61 | 0 |
| Auth.Application.Tests | 225 | 225 | 0 |
| Auth.Integration.Tests | 293 | 293 | 0 |

Antes desta spec eram 519 testes (61 + 189 + 269). Os testes de integração usam um SQL Server real em container (Testcontainers).

## Rastreabilidade

Caminhos relativos a `test/auth-service/`. Os testes unitários ficam em `Auth.Application.Tests/UseCases/<CasoDeUso>/<CasoDeUso>InteractorTests.cs`, os de integração em `Auth.Integration.Tests/`.

### Decisões da spec

| Decisão | Teste | Resultado |
|---|---|---|
| 1. Tabela `auth.audit_events`, só inserção, colunas da spec, `CHECK` de `event_type` e `outcome`, índices | `Api/AuditTrailApiTests.cs::ShouldRejectAnUnknownEventTypeOrOutcomeAtTheDatabase` (2 casos, erro `547`), `::ShouldIndexTheTableByUserAndTimeAndByTime`, `::ShouldKeepTheAuditEventsOfADeletedAccountBecauseThereIsNoForeignKey` | Passou |
| 2. `UserRegistered` | `RegisterUserInteractorTests::ShouldRecordTheUserRegisteredEventWithoutPasswordOrTokenWhenUserIsCreated`, `::ShouldRecordNoEventWhenTheEmailIsAlreadyTaken`, `::ShouldRecordNoEventWhenThePasswordIsRejected`; `AuditTrailApiTests::ShouldRecordUserRegisteredWhenTheRegistrationSucceeds`, `::ShouldRecordNothingWhenTheRegistrationIsRejectedOrTheEmailIsTaken` | Passou |
| 2. `EmailConfirmed` | `ConfirmEmailInteractorTests::ShouldRecordTheEmailConfirmedEventInsideTheUnitOfWork`, `::ShouldRecordNoEventWhenTheTokenIsInvalid`; `AuditTrailApiTests::ShouldRecordEmailConfirmedWhenTheTokenIsValid` | Passou |
| 2. `LoginSucceeded`, `LoginFailed`, `AccountLockedOut` | `LoginInteractorTests::ShouldRecordTheLoginSucceededEventWithTheSessionInsideTheUnitOfWork`, `::ShouldRecordTheLoginFailedEventWithTheUserAndTheInvalidPasswordReason`, `::ShouldRecordTheLoginFailedEventWithTheLockedOutReasonWhenTheAccountIsLocked`, `::ShouldRecordTheAccountLockedOutEventWhenTheLastAllowedAttemptFails`, `::ShouldRecordTheLoginFailedEventWithTheInactiveAccountReason`, `::ShouldRecordOnlyTheFailureAndNoSuccessWhenTheAccountIsLockedBetweenReadAndWrite`; `AuditTrailApiTests::ShouldRecordLoginSucceededWithTheSessionTheIpAndTheUserAgent`, `::ShouldRecordLoginFailedWithTheInvalidPasswordReason`, `::ShouldRecordTheFailuresTheLockoutAndTheLockedAttempt` | Passou |
| 2. `RefreshTokenReuseDetected`; refresh normal não é auditado | `RefreshAccessTokenInteractorTests::ShouldRecordTheReuseEventWithUserAndSessionButNoTokenWhenAnOldTokenIsReused`, `::ShouldNotAuditARegularRefreshNorAnExpiredToken`; `AuditTrailApiTests::ShouldRecordRefreshTokenReuseWithTheSessionAndNotAnOrdinaryRefresh` | Passou |
| 2. `Logout` e `LogoutAll` | `LogoutInteractorTests::ShouldRecordTheLogoutEventWithTheSessionInsideTheUnitOfWork`, `::ShouldRecordNoEventWhenTheTokenWasAlreadyRevokedOrExpired`; `LogoutAllInteractorTests::ShouldRecordTheLogoutAllEventInsideTheUnitOfWork`; `AuditTrailApiTests::ShouldRecordLogoutAndLogoutAllButNotAnIdempotentLogout` | Passou |
| 2. `PasswordResetRequested` (só quando a conta existe) e `PasswordResetCompleted` | `RequestPasswordResetInteractorTests::ShouldRecordThePasswordResetRequestedEventInsideTheUnitOfWorkWhenTheAccountExists`, `::ShouldRecordNoEventWhenTheAccountDoesNotExist`; `ResetPasswordInteractorTests::ShouldRecordThePasswordResetCompletedEventInsideTheUnitOfWork`, `::ShouldRecordNoEventWhenTheResetIsRejected`; `AuditTrailApiTests::ShouldRecordPasswordResetRequestedOnlyWhenTheAccountExistsAndThenCompleted` | Passou |
| 2. `PasswordChanged` | `ChangePasswordInteractorTests::ShouldRecordThePasswordChangedEventWithTheSessionInsideTheUnitOfWork`, `::ShouldRecordNoEventWhenTheCurrentPasswordIsWrongAndTheAccountIsNotLockedYet`, `::ShouldRecordTheAccountLockedOutEventWhenTheWrongPasswordLocksTheAccount`; `AuditTrailApiTests::ShouldRecordPasswordChangedWithTheCurrentSession` | Passou |
| 2. `UserDeleted` (com o Admin como ator) | `DeleteUserInteractorTests::ShouldRecordTheUserDeletedEventWithoutActorWhenTheUserDeletesItself`, `::ShouldRecordTheAdminAsTheActorWhenAnAdminDeletesAnotherAccount`, `::ShouldRecordNoEventWhenTheDeletionIsRejected`; `AuditTrailApiTests::ShouldRecordUserDeletedWithTheAdminAsActor`, `::ShouldRecordUserDeletedWithoutActorOnSelfDeletion` | Passou |
| 2. `RoleChanged` | não existe: a spec 2026092520 está adiada | Não se aplica |
| 3. Nunca entram senha, token, hash nem o login digitado numa conta inexistente | `LoginInteractorTests::ShouldNeverRecordTheTypedLoginNorThePasswordWhenTheAccountDoesNotExist`; os testes de evento de cadastro, reset e refresh acima verificam que o evento serializado não contém senha nem token; `AuditTrailApiTests::ShouldNeverStoreTheTypedLoginNorThePasswordWhenTheAccountDoesNotExist` (`user_external_id` nulo), `::ShouldNeverStoreAPasswordATokenOrAHashInAnyColumn` (concatena todas as colunas de todas as linhas) | Passou |
| 4. Sucesso na mesma transação da operação; falha em transação própria | `Interactor tests` de cada fluxo (`ShouldRollBackWhenTheAuditEventCannotBeRecorded` e variações, com o `FakeUnitOfWork` contando commits e rollbacks); `RefreshAccessTokenInteractorTests::ShouldRecordTheReuseEventOutsideAnyUnitOfWorkSoItSurvivesTheRollback`; `AuditTrailApiTests::ShouldRemoveTheSuccessEventWhenTheOperationIsRolledBack` (troca de senha com falha depois do comando), `::ShouldRemoveTheLoginSucceededEventWhenTheLoginIsRolledBack`, `::ShouldKeepTheFailureEventWhenTheOperationAroundItIsRolledBack` | Passou |
| 5. Gateway `IAuditLog` na Application; IP e user agent por um contexto da Api | `AuditTrailApiTests::ShouldRecordLoginSucceededWithTheSessionTheIpAndTheUserAgent`, `::ShouldTruncateTheUserAgentAt256Characters` | Passou |
| 6. Só inserção garantida pelo banco (`auth_service` com `INSERT` e `SELECT`) | `Persistence/DatabaseRolesAndMigrateTests.cs::ShouldLetTheServiceInsertAndReadAuditEventsButNeverUpdateOrDeleteThem` (`UPDATE` e `DELETE` negados com o erro `229`, `TRUNCATE` negado), `::ShouldLetTheMigratorRunTheRetentionDeleteOnOldAuditEvents` (o script de retenção do `auth_migrator` funciona) | Passou |
| 6. Função de purga descartada; retenção de 1 ano como tarefa operacional documentada | só documentada, sem código | Não se aplica |
| 7. LGPD: IP e user agent são dados pessoais, retenção de 1 ano | só documentada | Não se aplica |
| 8. Sem endpoint de consulta; índices por `(user_external_id, occurred_at)` e por `occurred_at` | `AuditTrailApiTests::ShouldIndexTheTableByUserAndTimeAndByTime` | Passou |

### Tarefa Dev

| Critério | Teste | Resultado |
|---|---|---|
| `IAuditLog` e contexto de requisição na Application, montado pela Api; cada evento registrado nos interactors, sucesso dentro da transação e falha fora | testes unitários e de integração das decisões 2 a 5 | Passou |

### Tarefa DBA

| Critério | Teste | Resultado |
|---|---|---|
| Migration de `auth.audit_events` com `CHECK`, índices e grants só de `INSERT`/`SELECT`; `DapperAuditLog` | `AuditTrailApiTests` (`CHECK`, índices, gravação), `DatabaseRolesAndMigrateTests` (grants, com o SQL real do init) | Passou |

### Tarefa Tester

| Critério | Teste | Resultado |
|---|---|---|
| Unitários: cada fluxo registra o evento certo; nenhum evento carrega senha, token ou login digitado de conta inexistente | testes unitários listados nas decisões 2 e 3 (`Fakes/FakeAuditLog.cs` guarda os eventos) | Passou |
| Integração: um rollback remove o evento de sucesso e mantém o de falha; `auth_service` não consegue `UPDATE` nem `DELETE` | `AuditTrailApiTests::ShouldRemoveTheSuccessEventWhenTheOperationIsRolledBack`, `::ShouldKeepTheFailureEventWhenTheOperationAroundItIsRolledBack`; `DatabaseRolesAndMigrateTests::ShouldLetTheServiceInsertAndReadAuditEventsButNeverUpdateOrDeleteThem` | Passou |

### Tarefa Tech Writer

| Critério | Artefato | Resultado |
|---|---|---|
| Doc com o catálogo de eventos, o que nunca é registrado, a retenção, a base LGPD, a retenção como tarefa operacional (com o script de exemplo) e consultas SQL de exemplo | `docs/auth/0011 - Auditoria de Eventos de Seguranca.md` (vinculado no `Ouroboros.slnx`) | Entregue |

## Observações

- **`DENY` sem saber o nome do papel.** O papel da API tem nome de ambiente (`AUTH_DB_USER`, e um nome aleatório nos testes). A migration descobre quem recebeu `UPDATE`/`DELETE` no schema `auth` (consulta a `sys.database_permissions`) e aplica o `DENY` no objeto a cada um. Num banco sem esse `GRANT` (o container dos testes, onde tudo roda como `sa`) o laço não faz nada.
- **Logout, logout-all e solicitação de recuperação** passaram a rodar numa `IUnitOfWork`, para o evento de sucesso ficar na mesma transação. A solicitação de recuperação agora também é atômica (invalidar os links anteriores, gravar o novo e auditar).
- **`AccountLockedOut` também na troca de senha e na exclusão de conta.** A spec lista o evento sem dizer onde. Registrei sempre que o `RecordFailedAccessAsync` bloqueia a conta, nos três fluxos que o usam (login, troca de senha e exclusão), para a trilha não perder um bloqueio causado fora do login.
- **Existente x conta inativa na recuperação.** `PasswordResetRequested` só é gravado quando o link é gerado (conta existente, ativa e com e-mail confirmado). Uma conta existente mas inativa não gera evento, como também não gera link.
- **Falha de gravação do evento de falha.** Os eventos de falha são gravados em autocommit e não são engolidos: se o banco falhar, a resposta vira `500`. A alternativa (engolir o erro) esconderia uma falha da auditoria.
- **Teste com o `AuthApiFixture`.** O reset do banco entre testes passou a limpar `auth.audit_events` também.
