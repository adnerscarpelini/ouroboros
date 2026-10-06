# Docker

## O que existe hoje

Um unico `docker-compose.yml` na raiz do monorepo, projeto/stack `ouroboros`, com os servicos `sqlserver`, `sqlserver-init`, `seq`, `auth-migrate` e `auth-service`.

- Imagem `mcr.microsoft.com/mssql/server:2022-latest`, edicao Developer (`MSSQL_PID=Developer`), com `ACCEPT_EULA=Y`. Sem `ACCEPT_EULA` o container encerra na hora com a mensagem sobre a licenca — por isso suba sempre via `docker compose`, nunca rodando a imagem direto.
- Uma unica instancia SQL Server compartilhada entre todos os microsservicos — nao um container por servico. O isolamento entre servicos vem do banco e do login, nao do container (ver `docs/project/0001 - Arquitetura.md`).
- Dados persistidos no volume nomeado `ouroboros-sqlserver-data`, sobrevive a `docker compose down` (so some com `down -v`).
- A imagem do SQL Server nao executa scripts de inicializacao sozinha. O servico `sqlserver-init` (uso unico, mesma imagem, `restart: "no"`) espera o `sqlserver` ficar saudavel e executa `docker/sqlserver/init/01-create-auth-db.sh` via `sqlcmd`. O script e idempotente e roda a cada `docker compose up`. O `auth-migrate` so roda depois que o init termina com sucesso, e o `auth-service` so sobe depois do `auth-migrate` (ambos com `service_completed_successfully`).
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
3. O `sqlserver-init` cria o banco `ouroboros_auth` e os dois papeis do auth-service (valores em `.env`, ver "Papeis de banco" abaixo). O servico `auth-migrate` aplica as migrations (DbUp) e termina. So entao o `auth-service` sobe: **a API nao roda DDL**.

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

## Papeis de banco (menor privilegio)

O auth-service usa dois papeis, criados por `docker/sqlserver/init/01-create-auth-db.sh`:

| Papel | Permissao | Quem usa |
|---|---|---|
| `auth_migrator` (`AUTH_MIGRATOR_USER`) | `db_owner` do banco do servico e dono do schema `auth`: faz o DDL | So o comando `migrate` (servico `auth-migrate`), pela connection string `ConnectionStrings__Migration` |
| `auth_service` (`AUTH_DB_USER`) | So DML (`SELECT`, `INSERT`, `UPDATE`, `DELETE`), via `GRANT` no schema `auth`, que vale tambem para as tabelas futuras. As colunas `IDENTITY` nao exigem permissao extra | A API, pela connection string `ConnectionStrings__Default` |

- O script cria o schema `auth` (dono `auth_migrator`) antes do `GRANT`. A primeira migration so cria o schema se ele faltar.
- As senhas dos dois papeis passam na politica do SQL Server (`CHECK_POLICY = ON`) e devem ser diferentes (ver `.env.example`).
- Como o `auth_service` nao tem DDL, uma tabela so de insercao (auditoria) pode ser garantida pelo banco, tirando o `UPDATE`/`DELETE` do papel.
- **Bancos de dev que ja existiam sao descartaveis.** Nao ha script de transferencia de ownership: depois de atualizar o `.env` com `AUTH_MIGRATOR_USER` e `AUTH_MIGRATOR_PASSWORD`, recrie o volume:
  ```
  docker compose down -v
  docker compose up -d
  ```

## Migrations: comando `migrate`

A API nunca aplica migrations no startup. A mesma imagem, chamada com `migrate`, roda o DbUp e sai:

```
dotnet Auth.Api.dll migrate
```

- Usa `ConnectionStrings:Migration` (papel `auth_migrator`). Sem ela, o comando falha com erro e codigo de saida diferente de zero.
- E idempotente: rodar de novo nao faz nada.
- No Compose, o servico `auth-migrate` roda antes e a API depende dele (`depends_on: condition: service_completed_successfully`). Isso tambem resolve a corrida entre replicas.
- Localmente, sem Compose: defina `ConnectionStrings__Migration` e rode `dotnet run --project auth-service/Auth.Api -- migrate` antes de subir a API.
- Os testes de integracao aplicam as migrations pelo `MigrationRunner` no fixture, antes de subir a API.

## Container sem root

O estagio final do `Dockerfile` usa `USER $APP_UID` (usuario `app` das imagens .NET 8+). A porta 8082 esta acima de 1024, entao nao precisa de privilegio.

## Estado de producao do auth-service

**O auth-service nao esta pronto para producao.** Enquanto nao existir envio de e-mail, os tokens de confirmacao de cadastro e de redefinicao de senha so sao logados quando o ambiente e `Development`. Em qualquer outro ambiente nada e logado, entao cadastro e reset **nao funcionam** fora de dev. A spec de envio de e-mail remove o log de vez e libera o uso fora de dev. O Swagger so existe em `Development`.

Ficou adiado, por nao existir ambiente de destino nem como testar aqui:

- `docker-compose.prod.yml` e `appsettings.Production.json` (dois perfis);
- Docker secrets (`AddKeyPerFile`) para senhas e chave JWT;
- TLS no proxy de borda;
- Seq em producao (sem porta publicada, API key e UI autenticada);
- `AllowedHosts` restrito ao dominio publico.

Voltam em spec propria quando houver para onde implantar.
