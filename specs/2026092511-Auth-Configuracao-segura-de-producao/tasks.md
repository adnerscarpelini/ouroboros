# 2026092511 - Configuracao segura de producao — Tarefas

- [ ] **Dev** — Criar `appsettings.Production.json` e `docker-compose.prod.yml` (sem portas publicadas para API e Seq, `ASPNETCORE_ENVIRONMENT=Production`, `AllowedHosts` restrito, Docker secrets)
- [ ] **Dev** — Carregar segredos com `AddKeyPerFile("/run/secrets", optional: true)` e falhar no startup se um segredo obrigatório faltar
- [ ] **Dev** — Logar os tokens de confirmação e de reset só quando o ambiente for `Development`
- [ ] **Dev** — Adicionar `USER $APP_UID` ao estágio final do Dockerfile
- [ ] **Dev** — Tirar `MigrationRunner.Run` do startup e criar o comando `migrate`; adicionar o serviço `auth-migrate` ao Compose, com a API dependendo dele
- [ ] **DBA** — Atualizar `docker/sqlserver/init/01-create-auth-db.sh` e `.env.example` com os papéis `auth_migrator` (DDL, dono) e `auth_service` (só DML, com `GRANT` no schema); escrever o script de transferência de ownership para bancos existentes
- [ ] **Tester** — Integração: Swagger indisponível fora de Development; token não logado fora de Development; startup falha sem segredo obrigatório; `auth_service` não consegue executar DDL
- [ ] **Tech Writer** — Atualizar `docs/project/0002 - Docker.md` com os perfis de dev e prod, o comando `migrate`, os papéis de banco, os segredos, a topologia TLS/proxy e o acesso ao Seq; registrar que o serviço não está pronto para produção sem envio de e-mail
