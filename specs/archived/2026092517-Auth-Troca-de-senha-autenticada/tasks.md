# 2026092517 - Troca de senha autenticada — Tarefas

- [x] **Dev** — Criar o caso de uso `ChangePassword` (reautenticação com bloqueio, política, senha diferente da atual, transação com hash, revogação das outras sessões e zeragem do contador)
- [x] **Dev** — Criar o endpoint `PUT /api/users/me/password` (`[Authorize]`), com o `sub` e o `sid` tirados do token, e a política de rate limit `password-change`; atualizar a collection Postman
- [x] **DBA** — Criar no `DapperRefreshTokenRepository` a revogação de todas as sessões ativas de um usuário exceto uma (`RevokeAllActiveByUserExceptSessionAsync`)
- [x] **Tester** — Unitários: sucesso; senha atual errada conta falha; conta bloqueada; senha nova igual à atual; senha fora da política; a sessão atual continua e as outras são revogadas; solicitante excluído → `401`
- [x] **Tester** — Integração: após a troca, o refresh da sessão atual funciona e o de outra sessão falha; `429` depois de 5 tentativas
- [x] **Tech Writer** — Criar doc em `docs/auth/` sobre a troca de senha (contrato, reautenticação, sessões encerradas, limites)
