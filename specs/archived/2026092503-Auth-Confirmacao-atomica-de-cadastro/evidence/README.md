# Evidências

## Execução

- Comando: `dotnet test Ouroboros.slnx`
- Data: 06/10/2026
- Resultado: 273 testes, 273 aprovados, 0 falhas. Saída bruta em `dotnet-test.txt`.

| Projeto | Total | Passou | Falhou |
|---|---|---|---|
| Auth.Domain.Tests | 50 | 50 | 0 |
| Auth.Application.Tests | 123 | 123 | 0 |
| Auth.Integration.Tests | 100 | 100 | 0 |

A suíte de integração usa um SQL Server real em container (Testcontainers), com Docker 29.7.2. Antes desta spec eram 259 testes: 116 de Application (agora 123) e 93 de integração (agora 100).

## Rastreabilidade

Caminhos relativos a `test/auth-service/`.

### Decisões da spec

| Decisão | Teste | Resultado |
|---|---|---|
| 1. Consumo condicional no banco, com prazo no SQL | `Auth.Integration.Tests/Persistence/RepositorySqlTests.cs::ShouldMarkTokenAsUsedOnlyOnce`, `::ShouldMarkTokenAsUsedInExactlyOneOfManyConcurrentCalls`, `::ShouldNotMarkExpiredTokenAsUsed`, `::ShouldNotMarkTokenAsUsedAfterItWasInvalidated` | Passou |
| 2. Consumo do token e ativação do usuário na mesma transação | `Auth.Application.Tests/UseCases/ConfirmEmail/ConfirmEmailInteractorTests.cs::ShouldConsumeTokenAndActivateUserInsideTheSameUnitOfWork`, `::ShouldRollBackWhenUserUpdateFails`; `Auth.Integration.Tests/Api/EmailConfirmationApiTests.cs::ShouldKeepTokenPendingAndUserInactiveWhenUserUpdateFails` | Passou |
| 3. Mensagem única `400 Invalid or expired confirmation token` | tabela abaixo | Passou |
| 4. Endpoint público com o limite `email-confirm` | `Auth.Integration.Tests/Auth/AuthenticationProtectionTests.cs::ShouldReturnTooManyRequestsWhenIpExceedsPolicyLimit` (caso `/api/users/confirm-email`, da spec 2026092501) | Passou |

### Tarefa Dev — consumo atômico e mensagem única

| Critério | Teste | Resultado |
|---|---|---|
| Token válido ativa o usuário e consome o token | `ConfirmEmailInteractorTests::ShouldConfirmEmailAndActivateUserWhenTokenIsValid` | Passou |
| Commit único na transação | `ConfirmEmailInteractorTests::ShouldConsumeTokenAndActivateUserInsideTheSameUnitOfWork` | Passou |
| Mesma mensagem: token vazio | `ConfirmEmailInteractorTests::ShouldThrowGenericMessageWhenTokenIsBlank` (2 casos); `EmailConfirmationApiTests::ShouldRejectEmptyTokenWithTheSameMessage` | Passou |
| Mesma mensagem: token inexistente | `ConfirmEmailInteractorTests::ShouldThrowGenericMessageWhenTokenDoesNotExist`; `EmailConfirmationApiTests::ShouldRejectUnknownToken` | Passou |
| Mesma mensagem: token de outro tipo | `ConfirmEmailInteractorTests::ShouldThrowGenericMessageWhenTokenIsOfAnotherType`; `EmailConfirmationApiTests::ShouldRejectPasswordResetTokenAsConfirmationToken` | Passou |
| Mesma mensagem: token expirado | `ConfirmEmailInteractorTests::ShouldThrowGenericMessageWhenTokenIsExpired`; `EmailConfirmationApiTests::ShouldRejectExpiredTokenAndKeepUserInactive` | Passou |
| Mesma mensagem: token já usado | `ConfirmEmailInteractorTests::ShouldThrowGenericMessageWhenTokenWasAlreadyUsed`; `EmailConfirmationApiTests::ShouldRejectSecondConfirmationWithTheSameToken` | Passou |
| Mesma mensagem: conta inexistente | `ConfirmEmailInteractorTests::ShouldThrowGenericMessageWhenUserDoesNotExist` | Passou |
| Mesma mensagem: conta excluída | `ConfirmEmailInteractorTests::ShouldThrowGenericMessageWhenUserWasDeleted`; `EmailConfirmationApiTests::ShouldRejectTokenOfDeletedAccountWithTheSameMessage` | Passou |
| Perde a disputa por outra requisição: mesma mensagem, usuário inativo | `ConfirmEmailInteractorTests::ShouldThrowGenericMessageAndKeepUserInactiveWhenAnotherRequestConsumedTheToken` | Passou |

### Tarefa DBA — `expires_at > @now` no `TryMarkAsUsedAsync`

| Critério | Teste | Resultado |
|---|---|---|
| Token expirado não é consumido | `RepositorySqlTests::ShouldNotMarkExpiredTokenAsUsed` | Passou |
| Token invalidado (conta excluída) não é consumido | `RepositorySqlTests::ShouldNotMarkTokenAsUsedAfterItWasInvalidated` | Passou |
| Uso único, inclusive sob concorrência | `RepositorySqlTests::ShouldMarkTokenAsUsedOnlyOnce`, `::ShouldMarkTokenAsUsedInExactlyOneOfManyConcurrentCalls` | Passou |
| Redefinição de senha, que usa o mesmo método, segue funcionando | `Auth.Integration.Tests/Api/PasswordResetApiTests.cs` (8 testes) | Passou |

### Tarefa Tester

| Critério | Teste | Resultado |
|---|---|---|
| Unitários: expirado, já usado, outro tipo e conta excluída com a mesma mensagem | `ConfirmEmailInteractorTests` (casos da tabela da tarefa Dev) | Passou |
| Integração: duas confirmações simultâneas, uma `200` e uma `400` | `EmailConfirmationApiTests::ShouldReturnOneOkAndOneBadRequestWhenSameTokenIsConfirmedConcurrently` (8 rodadas) | Passou |
| Integração: falha forçada na atualização do usuário, token continua pendente | `EmailConfirmationApiTests::ShouldKeepTokenPendingAndUserInactiveWhenUserUpdateFails` (falha antes e depois do comando, com nova tentativa que dá `200`) | Passou |

### Tarefa Tech Writer

| Critério | Artefato | Resultado |
|---|---|---|
| Garantia de uso único e mensagem genérica documentadas | `docs/auth/0001 - Confirmacao de Cadastro.md`, seções "Erros da confirmação" e "Uso único e atomicidade" | Entregue |

## Observações

- A leitura do usuário foi para depois do consumo do token, dentro da transação (`ConfirmEmailInteractor.ConfirmAsync`). O `DapperUserRepository.UpdateAsync` regrava a linha toda com a entidade lida e não filtra `deleted_at`. Se a leitura fosse anterior, uma exclusão confirmada no meio faria a confirmação reativar a conta excluída. A janela que sobra, entre a leitura e o `UpdateAsync` dentro da transação, é de milissegundos e não tem teste. A exclusão atômica (spec 2026092505) é quem fecha de vez.
- Os testes de integração foram rodados contra o `ConfirmEmailInteractor` e o `DapperTokenRepository` antigos (arquivos desfeitos temporariamente): 10 de 13 falharam, entre eles a corrida entre duas confirmações, o rollback com falha depois do comando e o `TryMarkAsUsedAsync` com prazo. O caso `FaultTiming.Before` do rollback passa nos dois códigos, porque nada chega a ser gravado antes da falha. Os testes unitários não compilam contra o código antigo, pois o construtor mudou.
