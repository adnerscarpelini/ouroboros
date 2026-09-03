# 0008 - Observabilidade

## Contexto

Num monolito, "onde isso quebrou?" se responde lendo um log. Com serviços separados por rede, uma única requisição do usuário atravessa vários processos, e nenhum deles sozinho conta a história inteira: o gateway sabe que encaminhou, o serviço sabe que respondeu 500, e ninguém sabe quanto tempo cada trecho levou.

O `ErrorLog` por serviço, que o projeto já tinha, registra **que** um erro aconteceu — mas cada serviço vê só o seu, e não há como ligar um erro no Auth à requisição que o originou no gateway.

## Decisão

Adotar **OpenTelemetry** para rastreamento distribuído, exportando por **OTLP** para um **Jaeger** local.

Jaeger em vez de uma solução caseira: uma "visão central de erros" feita à mão resolveria menos e custaria mais. E Jaeger em vez de Seq porque o problema aqui é *trace* (duração e encadeamento entre serviços), não busca em log — além de ser aberto e de container único.

## Os três mecanismos e o que cada um responde

| Mecanismo | Responde | Onde vive |
|---|---|---|
| `common.error_logs` | "Qual foi a exceção, com stack trace?" | Banco de cada serviço |
| `X-Correlation-Id` | "Qual identificador o usuário pode me passar?" | Cabeçalho HTTP, ida e volta |
| Trace (Jaeger) | "Por onde a requisição passou e onde ela demorou?" | Jaeger |

**Os três usam o mesmo identificador.** O gateway gera o `X-Correlation-Id` a partir do trace atual (`Activity.Current.TraceId`), e o `GlobalExceptionHandler` grava esse mesmo valor em `error_logs.trace_id`. Na prática:

- O usuário relata o id que veio no cabeçalho da resposta → cole em `http://localhost:16686/trace/<id>` e veja a requisição inteira.
- Um erro no banco → `SELECT trace_id FROM common.error_logs` → mesma consulta no Jaeger, com todo o SQL que rodou antes de falhar.

Não é coincidência nem convenção frágil: é um valor só, propagado por dois caminhos diferentes.

## O que é instrumentado

Cada host (`auth-api` e `api-gateway`) registra:

- **ASP.NET Core** — o span de entrada de cada requisição. `/health` é filtrado: o healthcheck do container bate a cada 10 segundos e afogaria o resto.
- **HttpClient** — as chamadas de saída. É por aqui que passa o encaminhamento do YARP, e é o que faz gateway e serviço aparecerem no mesmo trace.
- **Npgsql** (`AddSource("Npgsql")`, só nos serviços) — cada comando SQL vira um span filho, com duração.

A propagação entre serviços é o cabeçalho **W3C `traceparent`**, tratado pelas instrumentações. Nenhum código de aplicação passa identificador adiante.

Exemplo real de um `POST /api/auth/register`:

```
[api-gateway] POST /api/auth/register      1400 ms
  [api-gateway] POST                       1355 ms   ← encaminhamento do YARP
    [auth-api] POST api/auth/register      1311 ms
      [auth-api] postgresql                  10 ms
      [auth-api] postgresql                  22 ms
      [auth-api] postgresql                  15 ms
      ... (9 spans, a transação inteira)
```

O tempo de banco soma menos de 60 ms dos 1311 ms: o resto é o Argon2 gerando o hash da senha, que é lento **de propósito**. Sem o trace, esse número seria um palpite.

## Como usar

Interface do Jaeger: **http://localhost:16686**

- Buscar por serviço (`api-gateway`, `auth-api`), por operação, ou por duração mínima para achar o que está lento.
- Abrir um trace específico: `http://localhost:16686/trace/<traceId>`.

O Jaeger sobe junto da infraestrutura (`docker compose up -d`), fora do profile `apps`, porque as Apis rodando pela IDE também exportam para ele.

## Configuração

Chave `Otlp:Endpoint` no `appsettings.json` de cada host:

```json
"Otlp": { "Endpoint": "http://localhost:4317" }
```

No Compose ela é sobrescrita para `http://jaeger:4317`, o nome do container na rede interna.

**Se a chave estiver vazia, a aplicação sobe sem exportar nada**, em vez de encher o log com falhas de conexão. É o que permite rodar uma Api isolada, sem coletor de pé, sem ruído.

Observabilidade nunca é dependência de inicialização: nenhum serviço tem `depends_on` no Jaeger. Se o coletor estiver fora do ar, perdem-se traces — não requisições.

## O que ainda não existe

- **Métricas** (throughput, latência por percentil, uso de recursos). O Jaeger é só traces; métricas pedem um backend próprio, como Prometheus.
- **Logs correlacionados**. O `ILogger` de cada serviço ainda escreve no console do container, sem ligação automática com o trace.
- **Amostragem**. Hoje 100% das requisições são exportadas. Num volume real isso seria caro, e entraria uma política de amostragem.

## Consequências

- Dois pacotes de instrumentação e um exportador por host, mais um container de coletor.
- Traces ficam apenas em memória no Jaeger *all-in-one*: reiniciar o container apaga o histórico. É adequado para desenvolvimento; um ambiente de verdade precisaria de armazenamento persistente.
- O tempo medido inclui a instrumentação. A ordem de grandeza é confiável; microssegundos não são.
