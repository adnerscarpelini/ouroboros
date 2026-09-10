#!/bin/bash
# Banco e role do serviço de Notificações. Mesma regra do Auth: banco próprio, role própria, sem
# credencial que alcance o banco de outro serviço.
#
# Este script só roda em volume novo (o entrypoint de init não roda de novo só porque entrou um
# arquivo novo). Para aplicar numa instalação existente sem apagar os dados, ver
# docs/0002 - Setup do Banco de Dados Local.md.
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" <<-EOSQL
	CREATE ROLE notifications_service WITH LOGIN PASSWORD '$NOTIFICATIONS_DB_PASSWORD';
	CREATE DATABASE ouroboros_notifications OWNER notifications_service;

	REVOKE CONNECT, TEMPORARY ON DATABASE ouroboros_notifications FROM PUBLIC;
	GRANT CONNECT ON DATABASE ouroboros_notifications TO notifications_service;
EOSQL
