# 2026092507 - Rotacao atomica e deteccao de reuso de refresh token — Tarefas

- [x] **Dev** — Executar dentro do `IUnitOfWork` a revogação condicional e a inserção do sucessor com o mesmo `SessionId`, devolvendo o par só depois do commit
- [x] **Dev** — Implementar a detecção de reuso: token revogado → revogar a sessão fora da transação, log `Warning` e `401` genérico; token expirado → `401` sem revogação
- [x] **DBA** — Incluir `expires_at > @now` na condição do `TryRevokeAsync`; criar `RevokeAllActiveBySessionAsync(sessionId, now)` no `DapperRefreshTokenRepository`
- [x] **Tester** — Unitários: reuso revoga só a sessão afetada; expirado não revoga; o sucessor herda o `SessionId`; o access token mantém o `sid`
- [x] **Tester** — Integração: refresh simultâneo com o mesmo token → um sucesso e a sessão revogada; falha forçada na inserção do sucessor → o token antigo continua válido; outra sessão do mesmo usuário não é afetada
- [x] **Tech Writer** — Atualizar `docs/auth/0003 - Login e Tokens.md`: remover a limitação "duas escritas, sem transação", documentar a detecção de reuso e a exigência de serializar o refresh no cliente
