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

A suíte de integração usa um SQL Server real em container (Testcontainers), com Docker 29.7.2. Antes desta spec eram 273 testes: 123 de Application (agora 130) e 100 de integração (agora 107).

Na primeira execução, 1 teste novo falhou por erro do próprio teste (`Order()` ordena `NoContent` 204 antes de `BadRequest` 400, e eu tinha a ordem invertida). Corrigido; o comportamento da API estava certo.

## Rastreabilidade

Caminhos relativos a `test/auth-service/`.

### Decisões da spec

| Decisão | Teste | Resultado |
|---|---|---|
| 1. Uma transação: consumo condicional, troca do hash, revogação das sessões e zeragem do bloqueio | `Auth.Application.Tests/UseCases/ResetPassword/ResetPasswordInteractorTests.cs::ShouldRunAllWritesInsideTheSameUnitOfWork`, `::ShouldRollBackWhenSessionRevocationFails`; `Auth.Integration.Tests/Api/PasswordResetApiTests.cs::ShouldKeepTokenPasswordAndSessionsWhenSessionRevocationFails` | Passou |
| 1. Prazo conferido no SQL do consumo | `Auth.Integration.Tests/Persistence/RepositorySqlTests.cs::ShouldNotMarkExpiredTokenAsUsed` (spec 2026092503) | Passou |
| 2. Validações antes do consumo; senha rejeitada não consome o link | `ResetPasswordInteractorTests::ShouldNotOpenUnitOfWorkWhenNewPasswordIsRejected` (2 casos), `::ShouldKeepTokenPendingWhenNewPasswordIsRejected`; `PasswordResetApiTests::ShouldKeepTokenUsableWhenNewPasswordIsRejected`, `::ShouldRejectNewPasswordEqualToTheCurrentOne` | Passou |
| 3. O reset desbloqueia a conta | `ResetPasswordInteractorTests::ShouldClearLockoutWhenPasswordIsChanged`; `PasswordResetApiTests::ShouldUnlockAccountWhenPasswordIsReset`, `::ShouldKeepLockoutWhenResetIsRejected`; `Persistence/RepositorySqlTests.cs::ShouldClearLockoutEvenWhenAccountIsLocked`, `::ShouldNotTouchDeletedUserWhenClearingLockout` | Passou |
| 4. Limitação dos access tokens já emitidos | só documentada, sem mudança de código (tratada na spec 2026092510) | Não se aplica |

### Tarefa Dev — transação com validações antes do consumo

| Critério | Teste | Resultado |
|---|---|---|
| Troca a senha com token válido | `ResetPasswordInteractorTests::ShouldChangePasswordWhenTokenIsValid`, `::ShouldMarkTokenAsUsedWhenPasswordIsChanged`, `::ShouldRevokeAllActiveRefreshTokensWhenPasswordIsChanged` | Passou |
| Commit único | `ResetPasswordInteractorTests::ShouldRunAllWritesInsideTheSameUnitOfWork` | Passou |
| Falha na revogação desfaz tudo | `ResetPasswordInteractorTests::ShouldRollBackWhenSessionRevocationFails` | Passou |
| Perde a disputa pelo token: rollback e bloqueio intacto | `ResetPasswordInteractorTests::ShouldRollBackAndKeepLockoutWhenTokenIsUsedConcurrently`, `::ShouldThrowDomainExceptionWhenTokenIsUsedConcurrently` | Passou |
| Conta excluída depois da leitura: rollback com mensagem genérica | `ResetPasswordInteractorTests::ShouldRollBackWithGenericMessageWhenAccountWasDeletedAfterTokenWasRead` | Passou |
| Token inválido (vazio, inexistente, expirado, usado, outro tipo) e usuário inativo | `ResetPasswordInteractorTests` (casos já existentes, agora com `AssertNothingChanged` conferindo também o desbloqueio) | Passou |

### Tarefa DBA — prazo no `TryMarkAsUsedAsync`

Entregue na spec 2026092503 (método compartilhado). Esta spec só confirma que a redefinição usa o método com o prazo: `RepositorySqlTests::ShouldNotMarkExpiredTokenAsUsed` e `PasswordResetApiTests::ShouldRejectExpiredToken`. Além disso, entrou o `DapperUserRepository.ClearLockoutAsync`, coberto pelos dois testes de `RepositorySqlTests` da decisão 3.

### Tarefa Tester

| Critério | Teste | Resultado |
|---|---|---|
| Unitário: senha rejeitada não consome o token | `ResetPasswordInteractorTests::ShouldNotOpenUnitOfWorkWhenNewPasswordIsRejected` | Passou |
| Unitário: o reset zera o bloqueio | `ResetPasswordInteractorTests::ShouldClearLockoutWhenPasswordIsChanged` | Passou |
| Integração: duas redefinições simultâneas, só uma troca a senha | `PasswordResetApiTests::ShouldChangePasswordOnceWhenSameTokenIsConfirmedConcurrently` (8 rodadas: um `204`, um `400`, login com a senha nova funciona) | Passou |
| Integração: falha forçada na revogação, token, senha e sessões intactos | `PasswordResetApiTests::ShouldKeepTokenPasswordAndSessionsWhenSessionRevocationFails` (falha antes e depois do comando, com nova tentativa que dá `204`) | Passou |

### Tarefa Tech Writer

| Critério | Artefato | Resultado |
|---|---|---|
| Limitação "escritas separadas, sem transação" removida e desbloqueio documentado | `docs/auth/0004 - Recuperacao de Senha.md` (seção "Atomicidade", item de segurança sobre o desbloqueio) e `docs/auth/0008 - Protecao contra Tentativas de Autenticacao.md` | Entregue |

## Observações

- **Verificação contra o código antigo.** Desfiz temporariamente o `ResetPasswordInteractor` e rodei os testes de integração da redefinição: 3 de 13 falharam (rollback com falha antes e depois do comando, e o desbloqueio). O teste de duas redefinições simultâneas passa no código antigo, porque o consumo condicional (`TryMarkAsUsedAsync`) já existia desde a spec 2026092302. Ele fica como proteção contra regressão.
- **Método novo no gateway.** `IUserRepository.ClearLockoutAsync` foi acrescentado porque `TryResetFailedAccessAsync` só age fora do bloqueio e `UpdateAsync` não grava a contagem nem o bloqueio (e não deve: sobrescreveria o contador atômico do login). Os nove repositórios falsos dos testes de Application ganharam o método.
- **Releitura do usuário.** Como na spec 2026092503, o usuário é lido de novo dentro da transação, depois de consumir o token, porque o `UpdateAsync` regrava a linha toda e não filtra `deleted_at`. A janela restante entre a releitura e o `UpdateAsync` é de milissegundos e não tem teste.
