# Logging e Erros

## Decisão

Log de aplicação (incluindo erro/exceção) usa **Serilog** gerando log estruturado, com **Seq** como destino/visualizador — não uma tabela de erros no Postgres.

Por quê:

- Uma tabela de erros mistura log de aplicação com dado de negócio no mesmo banco, não tem níveis (`Debug`/`Warning`/`Error`) e não escala em volume de escrita.
- Serilog é a lib de logging estruturado padrão do ecossistema .NET, plugada via `ILogger<T>` (abstração do BCL — `Microsoft.Extensions.Logging.Abstractions` —, não do ASP.NET Core, então não fere a regra 1 da `ouroboros-dev` mesmo se usada fora da `Api`).
- Seq é o agregador/viewer mais usado especificamente no ecossistema .NET, free pra uso single-user (suficiente pra este projeto de estudo) e sobe como um container só.

## Seq no docker-compose

O serviço `seq` já está no `docker-compose.yml` da raiz, ao lado do `postgres`.

- Imagem `datalust/seq:latest`.
- UI web na porta `${SEQ_UI_PORT}` do host (padrão `8081`), mapeada pra porta `80` do container.
- Ingestão de log (onde o Serilog vai apontar via sink HTTP) na porta `${SEQ_INGESTION_PORT}` do host (padrão `5341`), mesma porta dentro e fora do container.
- Dados persistidos no volume nomeado `ouroboros-seq-data`, sobrevive a `docker compose down` (mesma regra do `ouroboros-postgres-data`, ver `docs/0002 - Docker.md`).
- O Seq exige senha de admin no primeiro start do volume — vem de `SEQ_FIRSTRUN_ADMINPASSWORD`, preenchida a partir de `SEQ_ADMIN_PASSWORD` no `.env`. Só vale pro bootstrap inicial: se você trocar a senha pela UI depois, o valor real passa a ser o que você definiu lá, não mais o do `.env` (só volta a valer se o volume `ouroboros-seq-data` for recriado do zero).

Setup local:

```
docker compose up -d seq
```

UI acessível em `http://localhost:8081` (ou a porta escolhida em `SEQ_UI_PORT`), login `admin`.

## Como está configurado no `auth-service`

Serviço de referência pra replicar em qualquer serviço novo (`Auth.Api/Program.cs`):

- `builder.Host.UseSerilog(...)` lê `Serilog:MinimumLevel` do `appsettings.json`/`appsettings.Development.json`, escreve sempre no console, e adiciona o sink do Seq só se `Seq:ServerUrl` estiver configurado (`Seq__ServerUrl` no `environment` do `docker-compose.yml`, apontando pro serviço `seq`).
- `app.UseSerilogRequestLogging()` loga toda requisição (`HTTP {Method} {Path} responded {Status}`) — isso sozinho já captura qualquer exceção não tratada que suba até o host, com stack trace completo, sem precisar de nenhum middleware extra.
- `GlobalExceptionHandler` (`Auth.Api/Middleware/GlobalExceptionHandler.cs`, implementa `IExceptionHandler`, registrado com `AddExceptionHandler<T>()` + `AddProblemDetails()` + `app.UseExceptionHandler()`) intercepta qualquer exceção não tratada antes dela vazar pro cliente: loga em `Error` com o path/método da requisição e devolve `500` com `{"error": "An unexpected error occurred."}` — nunca stack trace ou detalhe interno no corpo da resposta.
- `DomainException` continua tratada no próprio controller (`catch (DomainException e)`), mas agora loga em `Warning` com o motivo antes de devolver `400 {"error": e.Message}` — assim dá pra ver no Seq *por que* uma requisição foi rejeitada, não só que foi.

Convenção pra endpoints novos: sempre logar o `catch (DomainException e)` em `Warning` com a mensagem antes de devolver o `400`. O `GlobalExceptionHandler` é único por serviço (registrado uma vez em `Program.cs`) e cobre qualquer exceção não tratada automaticamente — não precisa repetir isso por controller.
