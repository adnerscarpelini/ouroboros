# 2026092501 - Protecao contra tentativas de autenticacao — Tarefas

- [x] **Dev** — Mover os limites de `RateLimitingConfiguration` para opções `RateLimiting:*` validadas no startup, mantendo os valores atuais como padrão
- [x] **Dev** — Criar as políticas `auth-login`, `auth-refresh` e `email-confirm` e aplicá-las aos endpoints de login, refresh e confirmação de e-mail
- [x] **Dev** — Configurar `UseForwardedHeaders` antes do rate limiter, com `KnownProxies`/`KnownNetworks` vindos de configuração e `ForwardLimit = 1`
- [x] **Dev** — Adicionar à entidade `User` o controle de falhas (`AccessFailedCount`, `LockoutEnd`, `IsLockedOut(now)`, registro de falha que bloqueia na 5ª, zeragem no sucesso) e aplicá-lo no login e na reautenticação da exclusão de conta
- [x] **Dev** — Garantir uma verificação de hash por tentativa, com hash fictício para login inexistente e conta bloqueada, e a mesma resposta `401` para login inexistente, senha errada e conta bloqueada
- [x] **Dev** — Atualizar a collection Postman com as respostas `429` dos novos limites
- [x] **DBA** — Migration com `access_failed_count integer NOT NULL DEFAULT 0` e `lockout_end datetimeoffset` em `auth.users`; persistir e ler os campos no `DapperUserRepository`, com incremento e bloqueio atômicos no próprio `UPDATE`
- [x] **Tester** — Unitários: a 5ª falha bloqueia; o bloqueio expira; o sucesso zera; conta bloqueada responde igual a senha errada sem verificar a senha real; login inexistente passa pelo hash fictício; a reautenticação da exclusão conta como falha
- [x] **Tester** — Integração: `429` nas três políticas novas; IP lido de `X-Forwarded-For` só quando vem de proxy confiável; falhas concorrentes contadas corretamente
- [x] **Tech Writer** — Criar doc em `docs/auth/` sobre proteção contra tentativas (limites, bloqueio, resposta genérica, proxies confiáveis) e atualizar `0003 - Login e Tokens.md`
