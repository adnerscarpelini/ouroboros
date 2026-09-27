# 2026092507 - Rotacao atomica e deteccao de reuso de refresh token

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

Na validação de maturidade do auth-service, a rotação do refresh token apareceu em duas escritas sem transação, e o reuso de um token já revogado era apenas rejeitado. O usuário aprovou tornar a rotação atômica e incluir a detecção de reuso agora, em vez de adiá-la.

## Análise

Depende de 2026092516 (unidade de trabalho) e de 2026092506 (`session_id`). A rotação da spec 2026092204 continua. Referências: RFC 9700 (OAuth 2.0 Security Best Current Practice), seção sobre proteção de refresh tokens, e o "refresh token reuse detection" do Auth0.

Decisões:
1. **Rotação atômica.** Numa transação: revogar o token atual de forma condicional (`revoked_at IS NULL AND expires_at > @now` no SQL) e inserir o sucessor com o mesmo `session_id`. O par novo só é devolvido depois do commit.
2. **Detecção de reuso.** Se o token apresentado existe mas já foi revogado, alguém está usando uma cópia. Pode ser o atacante ou o cliente legítimo depois de o atacante ter rotacionado antes. Nos dois casos:
   - revogar todos os tokens ativos daquela sessão (`session_id`), sem afetar as outras sessões do usuário;
   - registrar `Warning` com o externalId do usuário e o `session_id` (nunca o token);
   - responder `401 Invalid refresh token`, igual aos demais casos.

   Essa revogação roda fora da transação da rotação (2026092516, decisão 6), para não ser desfeita.
3. **Token expirado e não revogado** → só `401`, sem revogar a sessão. Expirar não é sinal de roubo.
4. **Sem janela de tolerância.** Dois refresh simultâneos com o mesmo token derrubam a sessão. Uma janela de tolerância reabriria exatamente a brecha que a detecção fecha. Os clientes devem serializar o refresh, e isso fica documentado.
5. **O novo access token leva o mesmo `sid`** da sessão.
6. **Logout seguido de reuso.** Um token revogado por logout e apresentado de novo também revoga a sessão. É inofensivo, porque a sessão já está encerrada.
7. O endpoint continua público: o refresh token é a credencial e nunca aparece em log nem em URL. Limite `auth-refresh` da 2026092501.

## Tarefas

- [ ] **Dev** — Executar dentro do `IUnitOfWork` a revogação condicional e a inserção do sucessor com o mesmo `SessionId`, devolvendo o par só depois do commit
- [ ] **Dev** — Implementar a detecção de reuso: token revogado → revogar a sessão fora da transação, log `Warning` e `401` genérico; token expirado → `401` sem revogação
- [ ] **DBA** — Incluir `expires_at > @now` na condição do `TryRevokeAsync`; criar `RevokeAllActiveBySessionAsync(sessionId, now)` no `DapperRefreshTokenRepository`
- [ ] **Tester** — Unitários: reuso revoga só a sessão afetada; expirado não revoga; o sucessor herda o `SessionId`; o access token mantém o `sid`
- [ ] **Tester** — Integração: refresh simultâneo com o mesmo token → um sucesso e a sessão revogada; falha forçada na inserção do sucessor → o token antigo continua válido; outra sessão do mesmo usuário não é afetada
- [ ] **Tech Writer** — Atualizar `docs/auth/0003 - Login e Tokens.md`: remover a limitação "duas escritas, sem transação", documentar a detecção de reuso e a exigência de serializar o refresh no cliente
