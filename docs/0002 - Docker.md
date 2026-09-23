# Docker

## O que existe hoje

Um unico `docker-compose.yml` na raiz do monorepo, projeto/stack `ouroboros`, com um servico por enquanto: `postgres`.

- Imagem `postgres:16-alpine`.
- Uma unica instancia Postgres compartilhada entre todos os microsservicos — nao um container por servico. O isolamento entre servicos vem do banco e da role, nao do container (ver `docs/0001 - Arquitetura.md`).
- Dados persistidos no volume nomeado `ouroboros-postgres-data`, sobrevive a `docker compose down` (so some com `down -v`).
- Scripts em `docker/postgres/init/` sao montados em `/docker-entrypoint-initdb.d` e rodam automaticamente **so na primeira subida** do volume (Postgres nao reexecuta se o volume ja existe).

## Setup local

1. Copie `.env.example` para `.env` na raiz e preencha as senhas e a chave JWT (ver `docs/0005 - Configuracao JWT.md`):
   ```
   cp .env.example .env
   ```
2. Suba a stack:
   ```
   docker compose up -d
   ```
3. Na primeira subida, `docker/postgres/init/01-create-auth-db.sh` cria o banco `ouroboros_auth` e a role `auth_service` (valores em `.env`), e revoga `CONNECT`/`TEMPORARY` de `PUBLIC` nesse banco.

`.env` nunca e commitado (esta no `.gitignore`). `.env.example` documenta as variaveis necessarias e e o unico dos dois que fica versionado.

## Comandos principais

```
# subir a stack em background
docker compose up -d

# ver status dos containers
docker compose ps

# acompanhar logs do postgres
docker compose logs -f postgres

# parar os containers sem apagar dados
docker compose down

# parar e apagar o volume de dados (reseta o banco do zero)
docker compose down -v

# abrir um psql dentro do container, como superusuario
docker compose exec postgres psql -U ${POSTGRES_SUPERUSER} -d postgres

# abrir um psql direto no banco do auth-service, com a role dele
docker compose exec postgres psql -U ${AUTH_DB_USER} -d ${AUTH_DB_NAME}
```

## Adicionando o banco de um novo microsservico

Cada servico novo com persistencia ganha seu proprio script de bootstrap, seguindo `01-create-auth-db.sh`:

1. Crie `docker/postgres/init/NN-create-<servico>-db.sh` (proximo numero sequencial), criando a role e o banco `ouroboros_<servico>` e revogando `CONNECT`/`TEMPORARY` de `PUBLIC`.
2. Adicione as variaveis de credencial desse banco (`<SERVICO>_DB_NAME`, `<SERVICO>_DB_USER`, `<SERVICO>_DB_PASSWORD`) ao `.env.example` e repasse elas pro `environment` do servico `postgres` no `docker-compose.yml`.
3. Scripts `.sh` precisam de final de linha LF (o `.gitattributes` da raiz ja garante isso pra qualquer `*.sh` novo).

Esses scripts so rodam automaticamente se o volume `ouroboros-postgres-data` ainda nao existir. Num ambiente onde o Postgres ja esta de pe, aplique o script manualmente (`docker compose exec -T postgres psql -U ${POSTGRES_SUPERUSER} -d postgres < docker/postgres/init/NN-create-<servico>-db.sh` nao funciona direto porque o script usa variaveis de ambiente do container — rode o SQL equivalente a mao nesse caso).

Detalhes de nomenclatura de banco/schema/role ficam na skill `ouroboros-dba`, nao aqui.

## Cada microsservico vira sua propria imagem Docker

Toda API de microsservico do Ouroboros (`auth-service` e os que vierem depois) precisa ter seu proprio `Dockerfile`, capaz de gerar uma imagem que roda sozinha (`docker run`), sem depender do ambiente de desenvolvimento local (SDK do .NET, IDE, etc.) — so a imagem publicada e as variaveis de ambiente de configuracao (connection string, porta).

Quando um servico ganha esse `Dockerfile`, ele entra como um novo `service` neste `docker-compose.yml`, do mesmo jeito que `postgres` ja entra hoje. Esse checklist (criar o `Dockerfile`, adicionar o servico ao compose) faz parte do checklist de "criar um microsservico novo" na skill `ouroboros-dev`.
