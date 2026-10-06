# Evidências

## Execução

- Comando: `dotnet test Ouroboros.slnx`
- Data: 06/10/2026
- Resultado: 469 testes, 469 aprovados, 0 falhas. Saída bruta em `dotnet-test.txt`.

| Projeto | Total | Passou | Falhou |
|---|---|---|---|
| Auth.Domain.Tests | 61 | 61 | 0 |
| Auth.Application.Tests | 189 | 189 | 0 |
| Auth.Integration.Tests | 219 | 219 | 0 |

Antes desta spec eram 452 testes (61 + 183 + 208). Os testes de integração usam um SQL Server real em container (Testcontainers).

## Rastreabilidade

Caminhos relativos a `test/auth-service/`.

### Decisões da spec

| Decisão | Teste | Resultado |
|---|---|---|
| 1. `ExpiredTokenCleanupService` (`BackgroundService`), a cada 1 h | `Auth.Integration.Tests/Persistence/TokenCleanupTests.cs::ShouldCleanInTheBackgroundAndLogOnlyCountsWhenTheServiceIsEnabled` (intervalo de 1 s no teste; o padrão de 1 h está em `::ShouldEnableTheCleanupByDefaultWithHourlyIntervalAndThirtyDaysOfRetention`) | Passou |
| 2. `sp_getapplock` entre réplicas descartado | só documentado; `::ShouldFinishWithoutErrorWhenTwoCleanupsRunAtTheSameTime` mostra que duas execuções juntas terminam sem erro | Passou |
| 3. Retenção de 30 dias; ativos, revogados não expirados e expirados recentes ficam | `TokenCleanupTests::ShouldDeleteRowsExpiredForMoreThanTheRetentionAndKeepEverythingElse`, `::ShouldKeepRevokedRefreshTokensThatHaveNotExpiredYetBecauseReuseDetectionNeedsThem`, `::ShouldNotTouchUsersOrOtherUsersActiveTokens`; `Auth.Application.Tests/UseCases/CleanupExpiredTokens/CleanupExpiredTokensInteractorTests.cs::ShouldDeleteOnlyRowsExpiredBeforeNowMinusRetention` | Passou |
| 4. Lotes de 1.000, uma transação curta por lote, laço até não sobrar | `CleanupExpiredTokensInteractorTests::ShouldUseBatchesOfOneThousandRows`, `::ShouldKeepDeletingUntilABatchComesBackShort`, `::ShouldAskOnceMoreWhenTheLastBatchWasExactlyFull`, `::ShouldCountEachTableSeparately`, `::ShouldStopBetweenBatchesWhenCancelled`; `TokenCleanupTests::ShouldProcessMoreRowsThanOneBatchInFull` (2.500 linhas por tabela) | Passou |
| 5. Índice em `expires_at` nas duas tabelas | migration `V20260930100000__AddExpiresAtIndexes.sql`, aplicada em todo teste de integração pelo `MigrationRunner` | Passou |
| 6. Configuração `TokenCleanup:Enabled`, `Interval` e `Retention` validada no startup; desligada nos testes, exceto nos da própria limpeza | `TokenCleanupTests::ShouldFailAtStartupWhenTheCleanupSettingsAreInvalid` (3 casos), `::ShouldEnableTheCleanupByDefaultWithHourlyIntervalAndThirtyDaysOfRetention`; `Infrastructure/AuthApiFactory.cs` desliga a limpeza | Passou |
| 7. Log `Information` por ciclo com contagens; falha em `Error` e nova tentativa no ciclo seguinte | `TokenCleanupTests::ShouldCleanInTheBackgroundAndLogOnlyCountsWhenTheServiceIsEnabled` (sem valor de token no log), `::ShouldLogErrorAndTryAgainInTheNextCycleWhenACycleFails` | Passou |
| 8. Cadastro abandonado não é afetado | a regra só olha tokens pendentes (`ExistsPendingByUserAsync`); apagar expirados não a muda. Sem teste novo | Não se aplica |
| 9. A purga da auditoria não roda neste job | sem mudança | Não se aplica |

### Tarefa Dev

| Critério | Teste | Resultado |
|---|---|---|
| `ExpiredTokenCleanupService` com laço de lotes, opções `TokenCleanup:*` validadas e logs de contagem | `CleanupExpiredTokensInteractorTests`, `TokenCleanupTests` | Passou |

### Tarefa DBA

| Critério | Teste | Resultado |
|---|---|---|
| Migration com índices em `expires_at` e exclusão em lote nos dois repositórios | `TokenCleanupTests::ShouldDeleteRowsExpiredForMoreThanTheRetentionAndKeepEverythingElse`, `::ShouldProcessMoreRowsThanOneBatchInFull`; `Persistence/SqlHintConventionTests.cs` (varre o SQL novo: `DELETE` sem hint) | Passou |

### Tarefa Tester

| Critério | Teste | Resultado |
|---|---|---|
| Integração: expirados há mais de 30 dias apagados; ativos, revogados não expirados e expirados recentes ficam | `TokenCleanupTests::ShouldDeleteRowsExpiredForMoreThanTheRetentionAndKeepEverythingElse`, `::ShouldKeepRevokedRefreshTokensThatHaveNotExpiredYetBecauseReuseDetectionNeedsThem` | Passou |
| Integração: duas execuções simultâneas terminam sem erro e sem apagar o que deve ficar | `TokenCleanupTests::ShouldFinishWithoutErrorWhenTwoCleanupsRunAtTheSameTime` (3.000 linhas por tabela, a soma das duas execuções é 3.000; rodado 4 vezes seguidas sem deadlock) | Passou |
| Integração: volume maior que um lote processado por inteiro | `TokenCleanupTests::ShouldProcessMoreRowsThanOneBatchInFull` | Passou |

### Tarefa Tech Writer

| Critério | Artefato | Resultado |
|---|---|---|
| Doc com retenção, agendamento, configuração e operação | `docs/auth/0010 - Limpeza de Tokens Expirados.md` (vinculado no `Ouroboros.slnx`), com remissão em `docs/auth/0003 - Login e Tokens.md` | Entregue |

## Observações

- **Lugar do serviço.** O `BackgroundService` fica em `Auth.Api/Services/`, porque `Microsoft.Extensions.Hosting` é do ASP.NET. A lógica de lotes está no caso de uso `CleanupExpiredTokens` (Application), testado com repositórios falsos. O caso de uso recebe um `CancellationToken` além do request, para o serviço parar entre dois lotes ao encerrar.
- **Sem deadlock nas execuções simultâneas.** Os dois `DELETE TOP` percorrem o índice na mesma ordem e se enfileiram. A regra de hints do projeto (nenhum hint em `DELETE`) foi mantida.
- **Teste instável corrigido na própria spec.** A primeira versão do teste do serviço em segundo plano falhava só na suíte completa, por duas razões: um teste de configuração subia o host com a limpeza ligada e deixava um ciclo rodando nos testes seguintes, e o teste conferia o log antes de ele ser escrito (o log sai logo depois do `DELETE`). Agora a configuração padrão é lida do `appsettings.json` sem subir o host, e o teste espera pelo log.
