# Evidências

## Execução

- Comando: `dotnet test Ouroboros.slnx`
- Data: 06/10/2026
- Resultado: 484 testes, 484 aprovados, 0 falhas. Saída bruta em `dotnet-test.txt`.

| Projeto | Total | Passou | Falhou |
|---|---|---|---|
| Auth.Domain.Tests | 61 | 61 | 0 |
| Auth.Application.Tests | 189 | 189 | 0 |
| Auth.Integration.Tests | 234 | 234 | 0 |

Antes desta spec eram 469 testes (61 + 189 + 219). Os testes de integração usam um SQL Server real em container (Testcontainers).

## Rastreabilidade

Caminhos relativos a `test/auth-service/`.

### Decisões da spec

| Decisão | Teste | Resultado |
|---|---|---|
| 1. `GET /health/live` sem checks e `GET /health/ready` com `SELECT 1` de 2 s | `Auth.Integration.Tests/Api/HealthApiTests.cs::ShouldRespondHealthyWithoutAuthenticationWhenTheDatabaseIsUp` (2 casos), `::ShouldKeepLiveHealthyAndMakeReadyUnavailableWhenTheDatabaseIsDown`, `::ShouldGiveUpAfterTwoSecondsWhenTheDatabaseAcceptsTheConnectionButNeverAnswers` (servidor que aceita a conexão e não responde: `503` em cerca de 2 s) | Passou |
| 2. Resposta só com o status, sem mensagem de exceção, connection string nem host | `HealthApiTests::ShouldNotLeakAnyInternalDetailInTheUnhealthyBody` | Passou |
| 3. Anônimos | `HealthApiTests::ShouldRespondHealthyWithoutAuthenticationWhenTheDatabaseIsUp` (sem token) | Passou |
| 3. Fora do rate limit | `HealthApiTests::ShouldNotApplyTheRateLimitToTheProbes` (15 chamadas do mesmo IP com limite de teste de 3, todas `200`) | Passou |
| 3. Fora do log de requisições em `Information`; só as falhas são logadas | `HealthApiTests::ShouldChooseTheRequestLogLevelByPathAndOutcome` (8 casos), `::ShouldKeepSuccessfulProbesOutOfTheRequestLogButLogFailures` (captura a saída de console do Serilog: nenhuma linha de `/health/*` com `200`, linha de `503` presente e linha de uma requisição comum presente) | Passou |
| 4. Exposição externa bloqueada no proxy de borda | só documentada, sem código (o proxy só existe com o ambiente de produção) | Não se aplica |
| 5. Sem `healthcheck` do auth-service no Compose | só documentado | Não se aplica |
| 6. E-mail fora da readiness | só documentado (o envio de e-mail não existe) | Não se aplica |

### Tarefa Dev

| Critério | Teste | Resultado |
|---|---|---|
| Health checks com a verificação do SQL Server, `/health/live` e `/health/ready` anônimos, fora do rate limit e só com status | `HealthApiTests` | Passou |
| Filtrar as requisições de health bem-sucedidas do `UseSerilogRequestLogging` | `HealthApiTests::ShouldChooseTheRequestLogLevelByPathAndOutcome`, `::ShouldKeepSuccessfulProbesOutOfTheRequestLogButLogFailures` | Passou |

### Tarefa Tester

| Critério | Teste | Resultado |
|---|---|---|
| `live` e `ready` com `200` e o banco no ar | `HealthApiTests::ShouldRespondHealthyWithoutAuthenticationWhenTheDatabaseIsUp` | Passou |
| `ready` com `503` e `live` com `200` com o banco parado | `HealthApiTests::ShouldKeepLiveHealthyAndMakeReadyUnavailableWhenTheDatabaseIsDown` (porta recusando conexão, como um SQL Server parado) | Passou |
| O corpo não contém detalhes internos | `HealthApiTests::ShouldNotLeakAnyInternalDetailInTheUnhealthyBody` | Passou |

### Tarefa Tech Writer

| Critério | Artefato | Resultado |
|---|---|---|
| Doc com a semântica de cada endpoint, o uso em sondas de orquestrador e a restrição de acesso externo | `docs/project/0005 - Verificacoes de Saude.md` (vinculado no `Ouroboros.slnx`) e pasta "Health" da collection Postman | Entregue |

## Observações

- **Banco "parado" nos testes.** Em vez de parar o container compartilhado (que os outros testes usam), a API de teste recebe uma connection string para uma porta que acabou de ser liberada. A conexão é recusada, como num SQL Server parado. O caso do banco que aceita a conexão e não responde usa um `TcpListener` que não responde nunca, e prova o timeout de 2 s.
- **Detalhe da falha só no log.** O `SqlServerHealthCheck` registra a exceção em `Error` e devolve `Unhealthy` sem descrição. Isso respeita a decisão 3 (só as falhas são logadas) e a decisão 2 (nada de detalhe na resposta).
- **Captura do log.** O `Program` usa Serilog com sink de console, então o teste redireciona `Console.Out` enquanto as requisições rodam. As outras classes de teste da coleção rodam em sequência, então não há leitura cruzada.
