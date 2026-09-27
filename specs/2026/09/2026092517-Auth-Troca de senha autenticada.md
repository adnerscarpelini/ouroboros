# 2026092517 - Troca de senha autenticada

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

Na validação de maturidade do auth-service, a maior lacuna funcional foi a falta de troca de senha para quem está logado. Hoje o único caminho é "esqueci a senha", por e-mail. O usuário aprovou criar a troca de senha autenticada.

## Análise

Extensão do `auth-service`, sem serviço novo. Depende de 2026092516 (unidade de trabalho), 2026092506 (`sid`), 2026092509 (política de senha) e 2026092501 (bloqueio de conta). Referências: OWASP Authentication Cheat Sheet (reautenticação para mudança de credencial) e o comportamento de Google e Microsoft, que encerram os outros dispositivos ao trocar a senha.

Decisões:
1. **`PUT /api/users/me/password`** com `[Authorize]` e body `{ currentPassword, newPassword }`. O `me` é o `sub` do token.
   - Não existe variante para outra conta. Um Admin não troca a senha de ninguém, por menor privilégio.
   - A recuperação de conta alheia continua sendo o reset por e-mail.
2. **Reautenticação pela senha atual.**
   - Senha errada → `401 Invalid credentials`, e a falha conta para o bloqueio (2026092501).
   - Conta bloqueada → a mesma resposta `401`.
3. **A nova senha segue a política da 2026092509** e precisa ser diferente da atual.
4. **Numa transação** (2026092516):
   - grava o novo hash e o `password_changed_at`;
   - revoga todas as sessões, **exceto a atual** (o `sid` do token);
   - zera o contador de falhas.
5. **Resposta `204`**, sem corpo. Solicitante inexistente ou excluído → `401 Invalid access token`.
6. **Rate limit `password-change`:** 5 por IP a cada 15 min.
7. **Logs:** `Information` com o externalId, nunca as senhas. Avisar o dono por e-mail sobre a troca é o padrão de mercado e fica como TODO para a spec de envio de e-mail.
8. **Limitação:** os access tokens das outras sessões valem até expirar (no máximo 15 min).

## Tarefas

- [ ] **Dev** — Criar o caso de uso `ChangePassword` (reautenticação com bloqueio, política, senha diferente da atual, transação com hash, revogação das outras sessões e zeragem do contador)
- [ ] **Dev** — Criar o endpoint `PUT /api/users/me/password` (`[Authorize]`), com o `sub` e o `sid` tirados do token, e a política de rate limit `password-change`; atualizar a collection Postman
- [ ] **DBA** — Criar no `DapperRefreshTokenRepository` a revogação de todas as sessões ativas de um usuário exceto uma (`RevokeAllActiveByUserExceptSessionAsync`)
- [ ] **Tester** — Unitários: sucesso; senha atual errada conta falha; conta bloqueada; senha nova igual à atual; senha fora da política; a sessão atual continua e as outras são revogadas; solicitante excluído → `401`
- [ ] **Tester** — Integração: após a troca, o refresh da sessão atual funciona e o de outra sessão falha; `429` depois de 5 tentativas
- [ ] **Tech Writer** — Criar doc em `docs/auth/` sobre a troca de senha (contrato, reautenticação, sessões encerradas, limites)
