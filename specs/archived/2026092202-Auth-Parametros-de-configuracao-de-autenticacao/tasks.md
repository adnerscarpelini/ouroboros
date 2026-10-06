# 2026092202 - Parametros de configuracao de autenticacao — Tarefas

- [x] **Dev** — Criar `JwtSettings` (ou `AuthSettings`) em `Auth.Api` com chave de assinatura, emissor, audiência, tempo de expiração do access token e do refresh token
- [x] **Dev** — Adicionar seção correspondente em `appsettings.json` / `appsettings.Development.json`, com a chave de assinatura vindo de variável de ambiente em produção (nunca commitada em texto puro)
- [x] **Dev** — Registrar `IOptions<JwtSettings>` em `UseCaseConfiguration`
- [x] **Tech Writer** — Documentar em `docs/` como configurar esses parâmetros em cada ambiente (dev vs. produção)
