# Evidências

## Execução

- Comando: `dotnet test Ouroboros.slnx`
- Data: 06/10/2026
- Resultado: 308 testes, 308 aprovados, 0 falhas. Saída bruta em `dotnet-test.txt`.

| Projeto | Total | Passou | Falhou |
|---|---|---|---|
| Auth.Domain.Tests | 50 | 50 | 0 |
| Auth.Application.Tests | 139 | 139 | 0 |
| Auth.Integration.Tests | 119 | 119 | 0 |

A suíte de integração usa um SQL Server real em container (Testcontainers), com Docker 29.7.2. Antes desta spec eram 300 testes: 137 de Application (agora 139) e 113 de integração (agora 119).

O teste de concorrência entre dois Admins (`ShouldLetOnlyOneOfTwoAdminsDeleteTheOtherWhenTheyActAtTheSameTime`, 8 rodadas) e os de lock foram rodados 12 vezes seguidas depois da correção do deadlock, sem nenhuma falha.

## Rastreabilidade

Caminhos relativos a `test/auth-service/`.

### Decisões da spec

| Decisão | Teste | Resultado |
|---|---|---|
| 1. Ações privilegiadas leem o perfil do banco | `Auth.Application.Tests/UseCases/GetUser/GetUserInteractorTests.cs::ShouldDecideByTheRoleStoredInTheDatabase`; `UseCases/DeleteUser/DeleteUserInteractorTests.cs::ShouldDecideByTheRoleStoredInTheDatabase`; `Auth.Integration.Tests/Api/UserQueryApiTests.cs::ShouldDenyDemotedAdminImmediatelyEvenWithTheOldTokenStillValid`, `::ShouldAllowPromotedUserImmediatelyEvenWithTheOldTokenStillValid`; `Api/AccountDeletionApiTests.cs::ShouldDenyDemotedAdminImmediatelyEvenWithTheOldTokenStillValid` | Passou |
| 2. "É o próprio usuário?" pelos dados do banco | `GetUserInteractorTests::ShouldCompareSelfWithTheLoginAndEmailStoredInTheDatabase`, `::ShouldNotQueryTargetAgainWhenRequesterSearchesSelf` | Passou |
| 3. Solicitante inexistente ou excluído → `401 Invalid access token` | `GetUserInteractorTests::ShouldThrowInvalidAccessTokenExceptionWhenRequesterDoesNotExist`, `::ShouldThrowInvalidAccessTokenExceptionWhenRequesterWasDeleted`; `DeleteUserInteractorTests::ShouldThrowInvalidAccessTokenExceptionWhenRequesterIsAlreadyDeleted`, `::ShouldThrowInvalidAccessTokenExceptionWhenRequesterDoesNotExist`; `UserQueryApiTests::ShouldReturnUnauthorizedWithGenericBodyWhenRequesterWasDeletedAndTokenIsStillValid`; `AccountDeletionApiTests::ShouldReturnUnauthorizedWhenRequesterWasDeletedAndTokenIsStillValid` | Passou |
| 4. Negar antes de consultar o alvo | `GetUserInteractorTests::ShouldDenyWithoutQueryingTargetWhenUserSearchesAnotherUserByExternalId` (e as variantes por login, e-mail e conta inexistente); `DeleteUserInteractorTests::ShouldDenyWithoutQueryingTargetWhenUserDeletesAnotherAccount` | Passou |
| 5. Versão de segurança por requisição descartada | só documentada, sem código | Não se aplica |
| 6. Limitação em outros serviços | documentada em `docs/auth/0005` | Entregue |
| 7. Correção do deadlock na contagem de Admins (2026092505) | `Persistence/RepositorySqlTests.cs::ShouldMakeASecondCountWaitUntilTheFirstTransactionEnds`, `::ShouldRefuseCountForUpdateOutsideUnitOfWork`, `::ShouldCountOnlyActiveAndNotDeletedAdmins`, `::ShouldCountOnlyActiveAndNotDeletedAdminsWhenCountingForUpdate`; `AccountDeletionApiTests::ShouldLetOnlyOneOfTwoAdminsDeleteTheOtherWhenTheyActAtTheSameTime` | Passou |

### Tarefas

| Tarefa | Evidência | Resultado |
|---|---|---|
| Dev — `GetUserInteractor` carrega o solicitante, decide pelo banco, `401` se não existir; `GetUserRequest` sem `RequesterLogin`, `RequesterEmail` e `RequesterRole` | `GetUserInteractorTests` (todos os casos de consulta, de negação e de `401`) | Passou |
| Dev — `DeleteUserInteractor` usa o `Role` do solicitante carregado; `DeleteUserRequest` sem `RequesterRole`; `UserController` ajustado | `DeleteUserInteractorTests`; `AccountDeletionApiTests` | Passou |
| Dev — trocar o `UPDLOCK, HOLDLOCK` por `sp_getapplock` | `RepositorySqlTests` e `AccountDeletionApiTests` listados na decisão 7 | Passou |
| Tester — unitários: negado com `User` no banco, solicitante excluído → `401`, negação sem consultar o alvo | `GetUserInteractorTests` e `DeleteUserInteractorTests` listados acima | Passou |
| Tester — integração: rebaixado perde na hora, promovido ganha na hora, excluído → `401`, corrida entre Admins, espera pelo lock | `UserQueryApiTests`, `AccountDeletionApiTests` e `RepositorySqlTests` listados acima | Passou |
| Tech Writer — `docs/auth/0005`, `0006` e `0007` | privilégio decidido pelo banco, limitação em outros serviços e o lock do último Admin (`sp_getapplock`) | Entregue |

## Observações

- **Deadlock achado e corrigido.** Durante esta spec o teste de concorrência da exclusão de Admins falhou com `500` em cerca de 1 de cada 60 rodadas. O log mostrou `SqlException` 1205 (deadlock) no `UpdateAsync` do usuário dentro da transação: o `UPDLOCK, HOLDLOCK` da spec 2026092505 varria a tabela e seus locks de faixa entravam em ciclo com outras escritas nas mesmas linhas. A contagem agora usa `sp_getapplock` com dono `Transaction` (sem lock de linha, sem ciclo possível) e recusa ser chamada fora de uma `IUnitOfWork`. Corrigido aqui, sem spec nova, por decisão do usuário. A spec 2026092505 arquivada continua descrevendo o `UPDLOCK, HOLDLOCK`; o estado atual está em `docs/auth/0007` e na decisão 7 desta spec.
- **Desfecho novo e legítimo da corrida entre Admins.** Quem chega depois do commit da outra exclusão já foi excluído e recebe `401 Invalid access token`, em vez de `400`. O teste afirma a propriedade que importa: exatamente um `204`, o outro `400` ou `401`, nunca `500`, e sobra um Admin ativo. O teste de corrida antes afirmava só `[204, 400]` e ficava sujeito ao tempo de cada requisição.
- **Os testes novos não foram rodados contra o código antigo.** Não dá para compilá-los contra ele: os requests `GetUserRequest` e `DeleteUserRequest` mudaram de assinatura. Os testes de integração de rebaixamento são o equivalente funcional: com o código antigo, que decidia pelo claim, o Admin rebaixado continuaria recebendo `200`.
- **Alvo lido de novo na transação**, como nas specs 2026092503, 2026092504 e 2026092505, continua valendo.
