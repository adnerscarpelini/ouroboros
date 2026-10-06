# Evidências

## Execução

- Comando: `dotnet test Ouroboros.slnx`
- Data: 06/10/2026
- Resultado: 300 testes, 300 aprovados, 0 falhas. Saída bruta em `dotnet-test.txt`.

| Projeto | Total | Passou | Falhou |
|---|---|---|---|
| Auth.Domain.Tests | 50 | 50 | 0 |
| Auth.Application.Tests | 137 | 137 | 0 |
| Auth.Integration.Tests | 113 | 113 | 0 |

A suíte de integração usa um SQL Server real em container (Testcontainers), com Docker 29.7.2. Antes desta spec eram 287 testes: 130 de Application (agora 137) e 107 de integração (agora 113).

Os testes de concorrência (`ShouldLetOnlyOneOfTwoAdminsDeleteTheOtherWhenTheyActAtTheSameTime`, 8 rodadas, e `ShouldMakeASecondCountWaitUntilTheFirstTransactionEnds`) foram rodados 4 vezes seguidas, sem falha nem deadlock.

## Rastreabilidade

Caminhos relativos a `test/auth-service/`.

### Decisões da spec

| Decisão | Teste | Resultado |
|---|---|---|
| 1. Uma transação: exclusão lógica, revogação dos refresh tokens e invalidação dos tokens pendentes | `Auth.Application.Tests/UseCases/DeleteUser/DeleteUserInteractorTests.cs::ShouldDeleteEverythingInsideTheSameUnitOfWork`, `::ShouldRollBackWhenSessionRevocationFails`; `Auth.Integration.Tests/Api/AccountDeletionApiTests.cs::ShouldKeepAccountSessionsAndTokensWhenSessionRevocationFails` | Passou |
| 2. Último Admin sob concorrência com `UPDLOCK, HOLDLOCK` | `AccountDeletionApiTests::ShouldLetOnlyOneOfTwoAdminsDeleteTheOtherWhenTheyActAtTheSameTime`; `Persistence/RepositorySqlTests.cs::ShouldMakeASecondCountWaitUntilTheFirstTransactionEnds`, `::ShouldCountOnlyActiveAndNotDeletedAdminsWhenCountingForUpdate`; `DeleteUserInteractorTests::ShouldLockAdminCountOnlyWhenTargetIsActiveAdmin`, `::ShouldRollBackWhenDeletingLastActiveAdmin` | Passou |
| 3. Falha de senha conta para o bloqueio e fica fora da transação | `DeleteUserInteractorTests::ShouldNotOpenUnitOfWorkWhenPasswordIsWrong`; `AccountDeletionApiTests::ShouldCountWrongPasswordTowardLockoutEvenThoughDeletionFails` | Passou |
| 4. Autorização pelo perfil gravado no banco | não faz parte desta spec (2026092510) | Não se aplica |

### Tarefa Dev — transação com autorização e reautenticação antes

| Critério | Teste | Resultado |
|---|---|---|
| Exclusão da própria conta, de outra por Admin, revogação e invalidação dos tokens | `DeleteUserInteractorTests` (casos já existentes: `ShouldDeleteOwnAccountWhenPasswordIsCorrect`, `ShouldRevokeActiveRefreshTokensWhenUserIsDeleted`, `ShouldInvalidatePendingTokensWhenUserIsDeleted`, `ShouldDeleteAnotherAccountWhenRequesterIsAdmin`) | Passou |
| Commit único | `DeleteUserInteractorTests::ShouldDeleteEverythingInsideTheSameUnitOfWork` | Passou |
| Negar acesso, errar a senha ou apontar conta inexistente não abre transação | `DeleteUserInteractorTests::ShouldNotOpenUnitOfWorkWhenAccessIsDenied`, `::ShouldNotOpenUnitOfWorkWhenPasswordIsWrong` | Passou |
| Alvo relido na transação: se sumiu, `404` e rollback | `DeleteUserInteractorTests::ShouldRollBackWithNotFoundWhenTargetIsDeletedAfterFirstRead` | Passou |
| Falha forçada na revogação desfaz tudo | `AccountDeletionApiTests::ShouldKeepAccountSessionsAndTokensWhenSessionRevocationFails` (falha antes e depois do comando, com nova tentativa que dá `204`) | Passou |

### Tarefa DBA — contagem de Admins com bloqueio

| Critério | Teste | Resultado |
|---|---|---|
| `CountActiveAdminsForUpdateAsync` conta só Admins ativos e não excluídos | `RepositorySqlTests::ShouldCountOnlyActiveAndNotDeletedAdminsWhenCountingForUpdate`, `::ShouldCountOnlyActiveAndNotDeletedAdmins` | Passou |
| O lock dura até o fim da transação e uma segunda contagem espera | `RepositorySqlTests::ShouldMakeASecondCountWaitUntilTheFirstTransactionEnds` | Passou |

### Tarefa Tester

| Critério | Teste | Resultado |
|---|---|---|
| Integração: falha forçada na revogação, conta ativa e tokens intactos | `AccountDeletionApiTests::ShouldKeepAccountSessionsAndTokensWhenSessionRevocationFails` | Passou |
| Integração: dois Admins, cada um excluindo o outro ao mesmo tempo, um sucesso e um `400` | `AccountDeletionApiTests::ShouldLetOnlyOneOfTwoAdminsDeleteTheOtherWhenTheyActAtTheSameTime` (8 rodadas; confere o status `[204, 400]`, a mensagem do `400` e que sobra exatamente um Admin ativo) | Passou |

### Tarefa Tech Writer

| Critério | Artefato | Resultado |
|---|---|---|
| Garantia de consistência e regra concorrente do último Admin documentadas | `docs/auth/0007 - Exclusao de Conta.md`, seção "Atomicidade e último Admin" e "Limitações conhecidas" | Entregue |

## Observações

- **Verificação sem o código novo.** Dois experimentos temporários, ambos desfeitos: (1) sem `WITH (UPDLOCK, HOLDLOCK)` no SQL, `ShouldMakeASecondCountWaitUntilTheFirstTransactionEnds` e a corrida entre Admins falham; (2) sem a `IUnitOfWork` na chamada do interactor, a corrida entre Admins e os dois casos de rollback falham.
- **Método renomeado.** `CountActiveAdminsAsync` virou `CountActiveAdminsForUpdateAsync`, para o nome avisar que o método trava linhas e só serve dentro de uma transação. Os dez repositórios falsos de teste e o teste de SQL foram atualizados.
- **Alvo relido na transação.** Como nas specs 2026092503 e 2026092504, porque o `UpdateAsync` regrava a linha toda. O lock dos Admins é tomado antes da releitura, para ela enxergar o que a primeira exclusão confirmou.
- **Limitações que continuam, documentadas em `docs/auth/0007`:** dois Admins excluindo a mesma conta ao mesmo tempo podem os dois receber `204` (fechar exigiria ler o alvo com lock de linha), e há uma janela de milissegundos em que uma redefinição de senha confirmada poderia ser sobrescrita. Nenhuma das duas tem teste.
- **Lock de faixa.** Sem índice sobre o perfil, a contagem lê a tabela inteira e segura cadastros e logins por alguns milissegundos enquanto um Admin é excluído. Documentado; um índice filtrado nos Admins ativos estreitaria o lock se a tabela crescer.
