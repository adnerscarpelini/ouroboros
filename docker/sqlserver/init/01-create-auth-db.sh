#!/bin/bash
# Cria o banco e os dois papeis do auth-service na instancia SQL Server compartilhada (menor privilegio):
#   auth_migrator: dono do banco (db_owner), faz o DDL. So o comando "migrate" do auth-service usa esse papel.
#   auth_service:  so DML (SELECT, INSERT, UPDATE, DELETE) no schema auth, inclusive nas tabelas futuras. A API usa este.
# Idempotente: roda a cada "docker compose up" (a imagem do SQL Server nao executa scripts de init sozinha).
# Os testes de integracao leem o SQL deste arquivo (bloco EOSQL) pra provar que o auth_service nao consegue DDL.
set -euo pipefail

sqlcmd_sa() {
    /opt/mssql-tools18/bin/sqlcmd -C -b -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" "$@"
}

# Nenhum dos dois logins e db_owner do servidor: so do proprio banco (o auth_service nem isso).
sqlcmd_sa -v DB="${AUTH_DB_NAME}" -v MIGRATOR="${AUTH_MIGRATOR_USER}" -v MIGRATORPASSWORD="${AUTH_MIGRATOR_PASSWORD}" -v USR="${AUTH_DB_USER}" -v DBPASSWORD="${AUTH_DB_PASSWORD}" <<'EOSQL'
IF DB_ID('$(DB)') IS NULL
    CREATE DATABASE [$(DB)] COLLATE Latin1_General_100_CS_AS;

IF SUSER_ID('$(MIGRATOR)') IS NULL
    CREATE LOGIN [$(MIGRATOR)] WITH PASSWORD = '$(MIGRATORPASSWORD)', CHECK_POLICY = ON;

IF SUSER_ID('$(USR)') IS NULL
    CREATE LOGIN [$(USR)] WITH PASSWORD = '$(DBPASSWORD)', CHECK_POLICY = ON;
GO

USE [$(DB)];

IF USER_ID('$(MIGRATOR)') IS NULL
    CREATE USER [$(MIGRATOR)] FOR LOGIN [$(MIGRATOR)];

ALTER ROLE db_owner ADD MEMBER [$(MIGRATOR)];

IF USER_ID('$(USR)') IS NULL
    CREATE USER [$(USR)] FOR LOGIN [$(USR)];
GO

-- O schema nasce aqui, dono do migrator, pra existir antes do GRANT (a primeira migration so o cria se faltar).
IF SCHEMA_ID('auth') IS NULL
    EXEC('CREATE SCHEMA auth AUTHORIZATION [$(MIGRATOR)]');
GO

-- Permissao no schema vale tambem pras tabelas criadas depois. IDENTITY nao exige permissao extra.
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::auth TO [$(USR)];
GO
EOSQL
