#!/bin/bash
# Cria o banco logico e a role do auth-service na instancia Postgres compartilhada.
# Executado automaticamente pelo Postgres apenas na primeira subida do container
# (imagem oficial roda todo *.sh de /docker-entrypoint-initdb.d em ordem alfabetica).
set -euo pipefail

psql -v ON_ERROR_STOP=1 --username "${POSTGRES_USER}" --dbname "${POSTGRES_DB}" <<-EOSQL
    CREATE ROLE ${AUTH_DB_USER} WITH LOGIN PASSWORD '${AUTH_DB_PASSWORD}';
    CREATE DATABASE ${AUTH_DB_NAME} OWNER ${AUTH_DB_USER};

    -- O Postgres concede CONNECT/TEMPORARY a PUBLIC por padrao em todo banco novo.
    -- Sem revogar isso, a role de qualquer outro servico alcancaria este banco.
    REVOKE CONNECT, TEMPORARY ON DATABASE ${AUTH_DB_NAME} FROM PUBLIC;
EOSQL
