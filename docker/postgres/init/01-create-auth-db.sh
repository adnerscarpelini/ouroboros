#!/bin/bash
# Roda automaticamente na primeira subida do container (docker-entrypoint-initdb.d).
# Cria um banco lógico + role própria por serviço, restrita ao seu próprio banco —
# nenhum serviço usa uma credencial que alcance o banco de outro.
# Ver docs/0000 - Arquitetura.md, seção "Banco de dados".
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" <<-EOSQL
	CREATE ROLE auth_service WITH LOGIN PASSWORD '$AUTH_DB_PASSWORD';
	CREATE DATABASE ouroboros_auth OWNER auth_service;

	-- O PostgreSQL concede CONNECT a PUBLIC por padrão: sem revogar, qualquer role da instância
	-- (inclusive a de outro serviço) consegue se conectar a este banco. O isolamento entre serviços
	-- precisa ser real, não convenção. Ver https://www.postgresql.org/docs/current/ddl-priv.html
	REVOKE CONNECT, TEMPORARY ON DATABASE ouroboros_auth FROM PUBLIC;
	GRANT CONNECT ON DATABASE ouroboros_auth TO auth_service;
EOSQL
