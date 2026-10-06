# Evidências

## Execução

- Comando: `dotnet test Ouroboros.slnx`
- Data: 06/10/2026
- Resultado: 431 testes, 431 aprovados, 0 falhas. Saída bruta em `dotnet-test.txt`.

| Projeto | Total | Passou | Falhou |
|---|---|---|---|
| Auth.Domain.Tests | 61 | 61 | 0 |
| Auth.Application.Tests | 183 | 183 | 0 |
| Auth.Integration.Tests | 187 | 187 | 0 |

Antes desta spec eram 400 testes (61 + 168 + 171). Os testes de integração usam um SQL Server real em container (Testcontainers) e trocam o Pwned Passwords por um fake.

## Rastreabilidade

Caminhos relativos a `test/auth-service/`.

### Decisões da spec

| Decisão | Teste | Resultado |
|---|---|---|
| 1. `PUT /api/users/me/password` com `[Authorize]`, `me` = `sub`, sem variante para outra conta | `Auth.Integration.Tests/Api/PasswordChangeApiTests.cs::ShouldChangePasswordAndKeepOnlyTheCurrentSessionWhenCredentialsAreValid`, `::ShouldReturnUnauthorizedWithoutAccessToken`, `::ShouldNotExposeAVariantToChangeAnotherAccountsPassword` (um Admin recebe `404`), `::ShouldNotTouchOtherUsersSessionsOrPasswords`; `Auth.Application.Tests/UseCases/ChangePassword/ChangePasswordInteractorTests.cs::ShouldNotChangeAnyOtherUsersPassword` | Passou |
| 2. Reautenticação pela senha atual; senha errada conta para o bloqueio; conta bloqueada → mesma resposta `401` | `ChangePasswordInteractorTests::ShouldThrowInvalidCredentialsAndCountTheFailureWhenCurrentPasswordIsWrong`, `::ShouldThrowInvalidCredentialsWithoutCountingWhenAccountIsLockedOut`; `PasswordChangeApiTests::ShouldReturnUnauthorizedAndCountTheFailureWhenCurrentPasswordIsWrong`, `::ShouldLockTheAccountAfterRepeatedWrongCurrentPasswordsAndAnswerTheSame401` | Passou |
| 3. Nova senha segue a política e é diferente da atual | `ChangePasswordInteractorTests::ShouldRejectNewPasswordThatBreaksThePolicyWithoutCountingAsAFailure` (3 casos), `::ShouldRejectNewPasswordThatHasAppearedInADataBreach`, `::ShouldRejectNewPasswordEqualToTheCurrentOne`; `PasswordChangeApiTests::ShouldReturnBadRequestAndKeepEverythingWhenNewPasswordBreaksThePolicy` (3 casos), `::ShouldReturnBadRequestWhenNewPasswordHasAppearedInADataBreach`, `::ShouldReturnBadRequestWhenNewPasswordEqualsTheCurrentOne` | Passou |
| 4. Transação: novo hash e `password_changed_at`, revogação das outras sessões (exceto o `sid`), contador zerado | `ChangePasswordInteractorTests::ShouldChangePasswordRevokeOtherSessionsAndKeepTheCurrentOneWhenCredentialsAreValid`, `::ShouldRevokeEverySessionWhenTheAccessTokenHasNoSessionId`, `::ShouldRunAllWritesInsideTheSameUnitOfWorkAndClearTheLockout`, `::ShouldRollBackWhenRevokingTheOtherSessionsFails`; `PasswordChangeApiTests::ShouldZeroTheFailureCounterWhenThePasswordIsChanged`, `::ShouldKeepPasswordAndSessionsWhenRevokingTheOtherSessionsFails` (falha depois do comando) | Passou |
| 5. `204` sem corpo; solicitante inexistente ou excluído → `401 Invalid access token` | `ChangePasswordInteractorTests::ShouldThrowInvalidAccessTokenWhenUserDoesNotExist`, `::ShouldThrowInvalidAccessTokenWhenUserWasDeleted`, `::ShouldThrowInvalidAccessTokenWhenUserIsNotActive`; `PasswordChangeApiTests::ShouldReturnUnauthorizedInvalidAccessTokenWhenTheAccountWasDeleted` | Passou |
| 6. Rate limit `password-change`: 5 por IP a cada 15 min | `PasswordChangeApiTests::ShouldReturnTooManyRequestsWhenThePasswordChangeLimitIsExceeded` (limite de teste de 3, como nas outras políticas; o valor 5 está em `appsettings.json`) | Passou |
| 7. Logs com o externalId, nunca as senhas; aviso por e-mail como `TODO` | log `Information` no `UserController.ChangePassword`; `TODO` no `ChangePasswordInteractor`. Sem teste de log | Não se aplica |
| 8. Access tokens das outras sessões valem até expirar | `PasswordChangeApiTests::ShouldKeepAccessTokensOfOtherSessionsValidUntilTheyExpire` | Passou |

### Tarefa Dev

| Critério | Teste | Resultado |
|---|---|---|
| Caso de uso `ChangePassword` | `ChangePasswordInteractorTests` (13 testes + casos de `Theory`) | Passou |
| Endpoint, `sub` e `sid` do token, política `password-change` e collection Postman | `PasswordChangeApiTests`; request "Change Password" em `auth-service/Auth.Api/Postman/Auth.postman_collection.json` | Passou |

### Tarefa DBA

| Critério | Teste | Resultado |
|---|---|---|
| `DapperRefreshTokenRepository.RevokeAllActiveByUserExceptSessionAsync` | `PasswordChangeApiTests::ShouldChangePasswordAndKeepOnlyTheCurrentSessionWhenCredentialsAreValid`, `::ShouldNotTouchOtherUsersSessionsOrPasswords` | Passou |

### Tarefa Tester

| Critério | Teste | Resultado |
|---|---|---|
| Unitários: sucesso; senha atual errada conta falha; conta bloqueada; senha igual à atual; fora da política; sessão atual continua e as outras são revogadas; solicitante excluído → `401` | `ChangePasswordInteractorTests` | Passou |
| Integração: após a troca, o refresh da sessão atual funciona e o de outra sessão falha; `429` | `PasswordChangeApiTests::ShouldChangePasswordAndKeepOnlyTheCurrentSessionWhenCredentialsAreValid`, `::ShouldReturnTooManyRequestsWhenThePasswordChangeLimitIsExceeded` | Passou |

### Tarefa Tech Writer

| Critério | Artefato | Resultado |
|---|---|---|
| Doc sobre a troca de senha (contrato, reautenticação, sessões encerradas, limites) | `docs/auth/0009 - Troca de Senha.md` (vinculado no `Ouroboros.slnx`), com a linha da política `password-change` em `docs/auth/0004 - Recuperacao de Senha.md` | Entregue |

## Observações

- **Nome do método.** A tarefa DBA cita `RevokeAllActiveByUserExceptSessionAsync`; o gateway usa esse nome, e o parâmetro `Guid.Empty` significa "não preservar nenhuma sessão" (token sem `sid`).
- **Usuário inativo.** A spec só cita inexistente e excluído. Tratei também o usuário inativo (e-mail não confirmado) como `401 Invalid access token`, porque um token válido nunca pertence a conta inativa e o login já a recusa.
- **Falha de senha.** O contador usa o mesmo `RecordFailedAccessAsync` do login e da exclusão de conta, em autocommit, fora da transação.
