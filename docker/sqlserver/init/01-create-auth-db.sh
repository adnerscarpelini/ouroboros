#!/bin/bash
# Cria o banco, o login e o usuario do auth-service na instancia SQL Server compartilhada.
# Idempotente: roda a cada "docker compose up" (a imagem do SQL Server nao executa scripts de init sozinha).
set -euo pipefail

sqlcmd_sa() {
    /opt/mssql-tools18/bin/sqlcmd -C -b -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" "$@"
}

# Login e usuario nao ficam como db_owner do servidor: so dono do proprio banco.
sqlcmd_sa -v DB="${AUTH_DB_NAME}" -v USR="${AUTH_DB_USER}" -v DBPASSWORD="${AUTH_DB_PASSWORD}" <<'EOSQL'
IF DB_ID('$(DB)') IS NULL
    CREATE DATABASE [$(DB)] COLLATE Latin1_General_100_CS_AS;

IF SUSER_ID('$(USR)') IS NULL
    CREATE LOGIN [$(USR)] WITH PASSWORD = '$(DBPASSWORD)', CHECK_POLICY = ON;
GO

USE [$(DB)];

IF USER_ID('$(USR)') IS NULL
    CREATE USER [$(USR)] FOR LOGIN [$(USR)];

ALTER ROLE db_owner ADD MEMBER [$(USR)];
GO
EOSQL
