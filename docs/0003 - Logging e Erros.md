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

Setup local:

```
docker compose up -d seq
```

UI acessível em `http://localhost:8081` (ou a porta escolhida em `SEQ_UI_PORT`).

## O que falta

Serilog em si ainda não está configurado em nenhum serviço, porque nenhum serviço existe no repositório ainda (`auth-service` é só o banco, criado via `docker/postgres/init/01-create-auth-db.sh` — os 4 projetos `.csproj` ainda não foram criados).

A fiação do Serilog (pacotes NuGet, sink apontando pro Seq, nível de log por ambiente) entra em `Program.cs` do projeto `Api` de cada serviço, seguindo a mesma regra de que só a `Api` conhece detalhes de infraestrutura de logging — `Domain`/`Application`/`Infrastructure` só dependem de `ILogger<T>` quando precisarem logar algo, nunca do Serilog diretamente. Isso fica pra quando o `auth-service` for criado (checklist de novo microsserviço na skill `ouroboros-dev`).
