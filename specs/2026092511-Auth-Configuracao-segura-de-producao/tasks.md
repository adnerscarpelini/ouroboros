# 2026092511 - Configuracao segura de producao — Tarefas

- [ ] **Dev** — Logar os tokens de confirmação e de reset só quando o ambiente for `Development`
- [ ] **Dev** — Adicionar `USER $APP_UID` ao estágio final do Dockerfile
- [ ] **Dev** — Tirar `MigrationRunner.Run` do startup e criar o comando `migrate`; adicionar o serviço `auth-migrate` ao Compose, com a API dependendo dele; ajustar a fixture dos testes de integração para rodar o `MigrationRunner` explicitamente
- [ ] **DBA** — Atualizar `docker/sqlserver/init/01-create-auth-db.sh` e `.env.example` com os papéis `auth_migrator` (DDL, dono) e `auth_service` (só DML, com `GRANT` no schema); bancos existentes recriam o volume
- [ ] **Tester** — Integração: Swagger indisponível fora de Development; token não logado fora de Development; `auth_service` não consegue executar DDL
- [ ] **Tech Writer** — Atualizar `docs/project/0002 - Docker.md` com o comando `migrate`, os papéis de banco e a recriação do volume; registrar que o serviço não está pronto para produção sem envio de e-mail e listar o que ficou adiado (compose de produção, segredos, TLS/proxy, Seq e `AllowedHosts`)
