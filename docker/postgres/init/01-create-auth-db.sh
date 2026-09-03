#!/bin/bash
# Roda automaticamente na primeira subida do container (docker-entrypoint-initdb.d).
# Cria um banco lógico + role própria por serviço, restrita ao seu próprio banco —
# nenhum serviço usa uma credencial que alcance o banco de outro.
# Ver docs/0000 - Arquitetura.md, seção "Banco de dados".
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" <<-EOSQL
	CREATE ROLE auth_service WITH LOGIN PASSWORD '$AUTH_DB_PASSWORD';
	CREATE DATABASE ouroboros_auth OWNER auth_service;
EOSQL
