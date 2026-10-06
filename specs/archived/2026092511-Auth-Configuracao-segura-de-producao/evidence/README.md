# Evidências

## Execução

- Comando: `dotnet test Ouroboros.slnx`
- Data: 06/10/2026
- Resultado: 452 testes, 452 aprovados, 0 falhas. Saída bruta em `dotnet-test.txt`.

| Projeto | Total | Passou | Falhou |
|---|---|---|---|
| Auth.Domain.Tests | 61 | 61 | 0 |
| Auth.Application.Tests | 183 | 183 | 0 |
| Auth.Integration.Tests | 208 | 208 | 0 |

Antes desta spec eram 431 testes (61 + 183 + 187). Os testes de integração usam um SQL Server real em container (Testcontainers).

## Rastreabilidade

Caminhos relativos a `test/auth-service/`.

### Decisões da spec

Escopo reduzido na revisão de 06/10/2026: as decisões 1, 2, 3, 9 e 10 estão adiadas e não têm código nem teste.

| Decisão | Teste | Resultado |
|---|---|---|
| 1, 2, 3, 9, 10. Compose de produção, TLS, Docker secrets, Seq e `AllowedHosts` | adiadas (sem ambiente de destino) | Não se aplica |
| 4. Swagger só em Development | `Auth.Integration.Tests/Infrastructure/ProductionConfigurationTests.cs::ShouldServeSwaggerInDevelopment`, `::ShouldNotServeSwaggerOutsideDevelopment` (Production e Staging, `404` no JSON e na UI) | Passou |
| 5. Tokens de confirmação e de reset só logados em Development | `ProductionConfigurationTests::ShouldLogEmailConfirmationAndPasswordResetTokensInDevelopment` (controle positivo: o log aparece), `::ShouldNotLogEmailConfirmationOrPasswordResetTokensOutsideDevelopment` (Production e Staging: nenhum log de token, e os tokens continuam gravados no banco) | Passou |
| 6. Container sem root (`USER $APP_UID`) | verificado à mão: `docker build` da imagem e `docker run --entrypoint id` devolveram `uid=1654(app)`. Sem teste automatizado | Passou (manual) |
| 7. Migrations fora do startup, comando `migrate` | `Auth.Integration.Tests/Persistence/DatabaseRolesAndMigrateTests.cs::ShouldNotApplyMigrationsWhenTheApiStarts`, `::ShouldApplyMigrationsAndExitWhenTheApiIsCalledWithMigrate` (processo `dotnet Auth.Api.dll migrate`, saída `0`, tabelas e `SchemaVersions` criados), `::ShouldBeIdempotentWhenMigrateRunsTwice`, `::ShouldFailWhenMigrateHasNoMigrationConnectionString`; no container, `docker run ... migrate` sem a connection string falha com `Connection string 'Migration' is not configured` | Passou |
| 7. Serviço `auth-migrate` no Compose, com a API dependendo dele | verificado por `docker compose --env-file .env.example config -q` (sem erro). Subir a stack completa não foi feito: o `.env` local não tem as variáveis novas | Passou (parcial) |
| 8. `auth_migrator` faz DDL; `auth_service` só DML, inclusive nas tabelas futuras | `DatabaseRolesAndMigrateTests::ShouldLetTheMigratorRunDdlAndTheServiceOnlyDml`, `::ShouldRefuseDdlToTheServiceRole` (9 comandos: `CREATE`/`ALTER`/`DROP`/`TRUNCATE`/`CREATE INDEX`/`CREATE SCHEMA`/`DROP SCHEMA`/`GRANT`), `::ShouldGiveTheServiceDmlOnTablesCreatedAfterTheGrant`. O SQL dos papéis é o do próprio `docker/sqlserver/init/01-create-auth-db.sh` (bloco `EOSQL`), executado no container de teste | Passou |

### Tarefa Dev

| Critério | Teste | Resultado |
|---|---|---|
| Logar os tokens só em Development | `ProductionConfigurationTests` | Passou |
| `USER $APP_UID` no Dockerfile | verificação manual acima | Passou (manual) |
| `MigrationRunner.Run` fora do startup, comando `migrate`, serviço `auth-migrate`, fixture roda o `MigrationRunner` | `DatabaseRolesAndMigrateTests`; `AuthApiFixture` aplica as migrations antes de subir a API | Passou |

### Tarefa DBA

| Critério | Teste | Resultado |
|---|---|---|
| `01-create-auth-db.sh` e `.env.example` com `auth_migrator` (dono) e `auth_service` (só DML, com `GRANT` no schema) | `DatabaseRolesAndMigrateTests` (usa o SQL real do script) | Passou |

### Tarefa Tester

| Critério | Teste | Resultado |
|---|---|---|
| Swagger indisponível fora de Development | `ProductionConfigurationTests::ShouldNotServeSwaggerOutsideDevelopment` | Passou |
| Token não logado fora de Development | `ProductionConfigurationTests::ShouldNotLogEmailConfirmationOrPasswordResetTokensOutsideDevelopment` | Passou |
| `auth_service` não consegue executar DDL | `DatabaseRolesAndMigrateTests::ShouldRefuseDdlToTheServiceRole` | Passou |

### Tarefa Tech Writer

| Critério | Artefato | Resultado |
|---|---|---|
| `migrate`, papéis, recriação do volume, "não pronto para produção sem e-mail" e lista do que ficou adiado | `docs/project/0002 - Docker.md` (seções "Papeis de banco", "Migrations: comando `migrate`", "Container sem root" e "Estado de producao do auth-service"); `docs/project/0004 - Testes de Integracao.md` | Entregue |

## Observações

- **Schema criado pelo init.** O `GRANT ... ON SCHEMA::auth` exige que o schema exista, e as migrations só rodam depois do init. O script cria o schema `auth` (dono `auth_migrator`), e a primeira migration passou a criá-lo só se faltar (`IF SCHEMA_ID('auth') IS NULL`). Alterar uma migration já aplicada não afeta bancos existentes (o DbUp não confere checksum), e os bancos de dev são descartáveis.
- **Verificação do SQL do init.** O teste lê o bloco `EOSQL` do script, troca as variáveis do `sqlcmd` e separa os lotes por `GO`. Assim a prova de menor privilégio não depende de uma cópia do SQL.
- **`.env` local.** O `.env` de quem roda o projeto precisa das variáveis `AUTH_MIGRATOR_USER` e `AUTH_MIGRATOR_PASSWORD` e do volume recriado (`docker compose down -v`). Não alterei o `.env`, que não é versionado.
- **Captura de log nos testes.** O `UseSerilog` do `Program` ignora outros `ILoggerProvider`, então o teste troca a `ILoggerFactory`, de onde o `ILogger<T>` do controller sai. O teste de Development serve de controle positivo: sem ele, a ausência de log em Production poderia ser só um captor quebrado.
- **Imagem.** Dois serviços do Compose (`auth-migrate` e `auth-service`) usam a mesma imagem `ouroboros-auth-service`.
