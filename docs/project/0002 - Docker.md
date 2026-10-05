# Docker

## O que existe hoje

Um unico `docker-compose.yml` na raiz do monorepo, projeto/stack `ouroboros`, com os servicos `sqlserver`, `sqlserver-init`, `seq` e `auth-service`.

- Imagem `mcr.microsoft.com/mssql/server:2022-latest`, edicao Developer (`MSSQL_PID=Developer`), com `ACCEPT_EULA=Y`. Sem `ACCEPT_EULA` o container encerra na hora com a mensagem sobre a licenca — por isso suba sempre via `docker compose`, nunca rodando a imagem direto.
- Uma unica instancia SQL Server compartilhada entre todos os microsservicos — nao um container por servico. O isolamento entre servicos vem do banco e do login, nao do container (ver `docs/project/0001 - Arquitetura.md`).
- Dados persistidos no volume nomeado `ouroboros-sqlserver-data`, sobrevive a `docker compose down` (so some com `down -v`).
- A imagem do SQL Server nao executa scripts de inicializacao sozinha. O servico `sqlserver-init` (uso unico, mesma imagem, `restart: "no"`) espera o `sqlserver` ficar saudavel e executa `docker/sqlserver/init/01-create-auth-db.sh` via `sqlcmd`. O script e idempotente e roda a cada `docker compose up`; o `auth-service` so sobe depois que o init termina com sucesso (`service_completed_successfully`).
- A senha do `sa` (`SQLSERVER_SA_PASSWORD`) exige senha forte (minimo 8 caracteres, com maiuscula, minuscula e numero) — senao o container nem inicia. Ela e so de administracao: nenhuma aplicacao usa o `sa`.
- A connection string do `auth-service` usa `Encrypt=True;TrustServerCertificate=True`: o SQL Server do container usa um certificado autoassinado. Fora do ambiente local, use um certificado valido e remova o `TrustServerCertificate`.

## Setup local

1. Copie `.env.example` para `.env` na raiz e preencha as senhas e a chave JWT (ver `docs/auth/0002 - Configuracao JWT.md`):
   ```
   cp .env.example .env
   ```
2. Suba a stack:
   ```
   docker compose up -d
   ```
3. O `sqlserver-init` cria o banco `ouroboros_auth` e o login/usuario `auth_service` (valores em `.env`), dono apenas desse banco. O `auth-service` aplica as migrations (DbUp) ao iniciar.

`.env` nunca e commitado (esta no `.gitignore`). `.env.example` documenta as variaveis necessarias e e o unico dos dois que fica versionado.

## Comandos principais

```
# subir a stack em background
docker compose up -d

# ver status dos containers
docker compose ps

# acompanhar logs do sql server
docker compose logs -f sqlserver

# parar os containers sem apagar dados
docker compose down

# parar e apagar o volume de dados (reseta o banco do zero)
docker compose down -v

# abrir um sqlcmd dentro do container, como sa
docker compose exec sqlserver bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD"'

# abrir um sqlcmd direto no banco do auth-service, com o login dele
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U ${AUTH_DB_USER} -d ${AUTH_DB_NAME}
```

No `sqlcmd` interativo, cada lote so executa depois de uma linha com `GO`. Para uma GUI, conecte o SSMS ou o Azure Data Studio em `localhost,1433` (marque "Trust server certificate").

## Adicionando o banco de um novo microsservico

Cada servico novo com persistencia ganha seu proprio script de bootstrap, seguindo `01-create-auth-db.sh`:

1. Crie `docker/sqlserver/init/NN-create-<servico>-db.sh` (proximo numero sequencial), criando o banco `ouroboros_<servico>`, o login e o usuario do servico, dono apenas desse banco.
2. Adicione as variaveis de credencial desse banco (`<SERVICO>_DB_NAME`, `<SERVICO>_DB_USER`, `<SERVICO>_DB_PASSWORD`) ao `.env.example` e repasse elas pro `environment` do servico `sqlserver-init` no `docker-compose.yml`. O `entrypoint` do `sqlserver-init` hoje executa so o `01-create-auth-db.sh`: ajuste-o para executar todos os scripts da pasta, em ordem.
3. Scripts `.sh` precisam de final de linha LF (o `.gitattributes` da raiz ja garante isso pra qualquer `*.sh` novo).
4. A senha do login precisa passar na politica de senha do Windows/SQL Server (`CHECK_POLICY = ON`), ou o `CREATE LOGIN` falha.

Como o init e idempotente e roda a cada `docker compose up`, um script novo tambem vale para um ambiente que ja esta de pe — basta rodar `docker compose up -d`.

Detalhes de nomenclatura de banco/schema/login ficam na skill `ouroboros-dba`, nao aqui.

## Cada microsservico vira sua propria imagem Docker

Toda API de microsservico do Ouroboros (`auth-service` e os que vierem depois) precisa ter seu proprio `Dockerfile`, capaz de gerar uma imagem que roda sozinha (`docker run`), sem depender do ambiente de desenvolvimento local (SDK do .NET, IDE, etc.) — so a imagem publicada e as variaveis de ambiente de configuracao (connection string, porta).

Quando um servico ganha esse `Dockerfile`, ele entra como um novo `service` neste `docker-compose.yml`, do mesmo jeito que `sqlserver` ja entra hoje. Esse checklist (criar o `Dockerfile`, adicionar o servico ao compose) faz parte do checklist de "criar um microsservico novo" na skill `ouroboros-dev`.
