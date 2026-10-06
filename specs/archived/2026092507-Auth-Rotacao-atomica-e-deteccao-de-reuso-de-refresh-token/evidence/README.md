# Evidências

## Execução

- Comando: `dotnet test Ouroboros.slnx`
- Data: 06/10/2026
- Resultado: 400 testes, 400 aprovados, 0 falhas. Saída bruta em `dotnet-test.txt`.

| Projeto | Total | Passou | Falhou |
|---|---|---|---|
| Auth.Domain.Tests | 61 | 61 | 0 |
| Auth.Application.Tests | 168 | 168 | 0 |
| Auth.Integration.Tests | 171 | 171 | 0 |

Antes desta spec eram 387 testes (61 + 165 + 161). Os testes de integração usam um SQL Server real em container (Testcontainers).

## Rastreabilidade

Caminhos relativos a `test/auth-service/`.

### Decisões da spec

| Decisão | Teste | Resultado |
|---|---|---|
| 1. Rotação atômica (revogação condicional + sucessor na mesma transação; par só depois do commit) | `Auth.Application.Tests/UseCases/RefreshAccessToken/RefreshAccessTokenInteractorTests.cs::ShouldRunRotationInsideTheSameUnitOfWorkAndCommitOnce`, `::ShouldRollBackAndNotRevokeTheSessionWhenInsertingTheSuccessorFails`; `Auth.Integration.Tests/Api/RefreshRotationApiTests.cs::ShouldKeepTheOldTokenValidWhenInsertingTheSuccessorFails`, `::ShouldKeepTheOldTokenValidWhenInsertingTheSuccessorFailsAfterTheCommandRan` | Passou |
| 1. Condição `expires_at > @now` no SQL da revogação | `Auth.Integration.Tests/Persistence/RepositorySqlTests.cs::ShouldRefuseToRevokeATokenWhoseExpirationHasPassedAtTheRevocationDate`, `::ShouldRevokeWhenTheRevocationDateIsStillBeforeTheExpiration` | Passou |
| 2. Detecção de reuso: revoga só a sessão, `401 Invalid refresh token`, `Warning` com externalId e `session_id` | `RefreshAccessTokenInteractorTests::ShouldRevokeTheSessionAndRejectWithGenericMessageWhenTokenWasAlreadyRevoked`, `::ShouldRevokeOnlyTheSessionOfTheReusedTokenWhenTokenWasAlreadyRevoked`, `::ShouldRevokeTheSessionWhenTokenIsReusedAfterRotation`; `RefreshRotationApiTests::ShouldRevokeTheWholeSessionAndRespondWithGenericMessageWhenAnOldTokenIsReused`, `::ShouldNotAffectOtherSessionsOfTheSameUserWhenATokenIsReused`; `RepositorySqlTests::ShouldRevokeOnlyTheActiveTokensOfTheGivenSession` | Passou |
| 2. Revogação fora da transação da rotação | `RefreshAccessTokenInteractorTests::ShouldTreatLosingTheRaceForTheTokenAsReuseAndRevokeTheSession` (a revogação da sessão acontece depois do `FakeUnitOfWork` terminar); `RefreshRotationApiTests::ShouldGiveOneSuccessAndRevokeTheSessionWhenTheSameTokenIsRefreshedAtTheSameTime` | Passou |
| 3. Expirado e não revogado → só `401`, sem revogar | `RefreshAccessTokenInteractorTests::ShouldRejectWithoutRevokingTheSessionWhenTokenIsExpired`; `RefreshRotationApiTests::ShouldRejectAnExpiredTokenWithoutRevokingTheSession` | Passou |
| 4. Sem janela de tolerância: dois refresh simultâneos → um sucesso e a sessão revogada | `RefreshRotationApiTests::ShouldGiveOneSuccessAndRevokeTheSessionWhenTheSameTokenIsRefreshedAtTheSameTime` (5 rodadas, com um portão que segura as duas leituras até as duas terem lido) | Passou |
| 5. O novo access token leva o mesmo `sid` | `RefreshAccessTokenInteractorTests::ShouldKeepSessionIdClaimInTheNewAccessTokenWhenRefreshTokenIsRotated`; `Auth.Integration.Tests/Api/MultiSessionApiTests.cs::ShouldKeepTheSessionIdAcrossRefreshRotation` (spec 2026092506) | Passou |
| 6. Logout seguido de reuso revoga a sessão | `Auth.Application.Tests/UseCases/Logout/LogoutInteractorTests.cs::ShouldRejectRefreshWhenTokenWasRevokedByLogout`; `RefreshRotationApiTests::ShouldRevokeTheSessionWhenATokenRevokedByLogoutIsReused` | Passou |
| 7. Endpoint público, token nunca em log nem URL, limite `auth-refresh` | sem mudança; limite coberto por `Auth.Integration.Tests/Auth/AuthenticationProtectionTests.cs` (spec 2026092501). O log de reuso traz só externalId e `session_id` | Passou |
| 8. `GetByHashAsync` com `READPAST` deixa uma janela sem revogação | documentada em `docs/auth/0003 - Login e Tokens.md`; sem teste (janela de milissegundos). Ver Observações | Não se aplica |

### Tarefa Dev

| Critério | Teste | Resultado |
|---|---|---|
| Rotação dentro do `IUnitOfWork`, com o sucessor herdando o `SessionId` | `RefreshAccessTokenInteractorTests::ShouldInheritSessionIdInTheSuccessorTokenWhenRefreshTokenIsRotated`, `::ShouldRunRotationInsideTheSameUnitOfWorkAndCommitOnce` | Passou |
| Detecção de reuso (revogado → revoga a sessão fora da transação, `Warning`, `401` genérico; expirado → `401` sem revogar) | testes da decisão 2 e 3 | Passou |

### Tarefa DBA

| Critério | Teste | Resultado |
|---|---|---|
| `expires_at > @now` no `TryRevokeAsync` | `RepositorySqlTests::ShouldRefuseToRevokeATokenWhoseExpirationHasPassedAtTheRevocationDate` | Passou |
| `RevokeAllActiveBySessionAsync(sessionId, now)` | `RepositorySqlTests::ShouldRevokeOnlyTheActiveTokensOfTheGivenSession` | Passou |

### Tarefa Tester

| Critério | Teste | Resultado |
|---|---|---|
| Unitários: reuso revoga só a sessão afetada; expirado não revoga; o sucessor herda o `SessionId`; o access token mantém o `sid` | `RefreshAccessTokenInteractorTests` (testes acima) | Passou |
| Integração: refresh simultâneo → um sucesso e a sessão revogada; falha na inserção do sucessor → o token antigo continua válido; outra sessão do mesmo usuário não é afetada | `RefreshRotationApiTests::ShouldGiveOneSuccessAndRevokeTheSessionWhenTheSameTokenIsRefreshedAtTheSameTime`, `::ShouldKeepTheOldTokenValidWhenInsertingTheSuccessorFails`, `::ShouldNotAffectOtherSessionsOfTheSameUserWhenATokenIsReused` | Passou |

### Tarefa Tech Writer

| Critério | Artefato | Resultado |
|---|---|---|
| Limitação "duas escritas, sem transação" removida; detecção de reuso e serialização do refresh no cliente documentadas | `docs/auth/0003 - Login e Tokens.md` (seções "Política de rotação" e "Detecção de reuso") e request "Refresh Access Token" da collection Postman | Entregue |

## Observações

- **Mensagens.** Token já revogado passou de `Refresh token has been revoked` para `Invalid refresh token` (decisão 2: igual aos demais casos). Token expirado continua `Refresh token has expired`. O teste de integração antigo de rotação foi ajustado: reusar o token antigo agora derruba a sessão, então o sucessor também deixa de valer.
- **Decisão 8 (`READPAST`).** Implementei a spec como está: a leitura continua com `READPAST`, e a janela em que a segunda requisição simultânea não enxerga a linha travada fica sem revogação da sessão. O teste de disputa usa um repositório que segura as duas leituras até as duas terem lido, o que exercita o caminho da detecção sem depender de sorte. Se a janela não for aceitável, a decisão volta para você: a alternativa citada na spec é ler sem `READPAST` dentro da transação da rotação.
- **Exceção nova.** `RefreshTokenReuseException` (subclasse de `InvalidRefreshTokenException`) leva o externalId e o `session_id` até o `AuthController`, que faz o log `Warning`. A Application não tem logger.
- **Índice.** `RevokeAllActiveBySessionAsync` filtra só por `session_id`, e o índice da 2026092506 é `(user_id, session_id)`. Para o volume atual e por ser um evento raro, não criei outro índice.
