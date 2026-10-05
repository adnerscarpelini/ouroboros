# 2026092520 - Gestao de perfil de acesso

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

Na validação de maturidade do auth-service, promover ou rebaixar um Admin apareceu como algo possível só por SQL manual (`docs/auth/0005 - Perfis de Acesso.md`), sem endpoint e sem controle. O usuário aprovou criar a troca de perfil por um fluxo autenticado e decidir como o primeiro Admin é criado.

## Análise

Extensão do `auth-service`. Depende de 2026092516 (unidade de trabalho), 2026092510 (privilégio lido do banco), 2026092505 (bloqueio do último Admin) e 2026092501 (bloqueio de conta). Referência: a atribuição de papéis por API administrativa do Keycloak e do Entra ID.

Decisões:
1. **`PUT /api/users/{externalId}/role`** com `[Authorize]` e body `{ role, password }`.
2. **Só Admin pode trocar perfil**, com o perfil lido do banco (2026092510). Um não-Admin recebe `403` antes de o alvo ser consultado.
3. **Reautenticação do Admin pela senha.** Senha errada → `401`, e a falha conta para o bloqueio (2026092501).
4. **Regras do alvo e do valor:**
   - inexistente ou excluído → `404`;
   - e-mail não confirmado → `400`;
   - `role` aceita só `User` ou `Admin` (allowlist). Outro valor → `400`;
   - valor igual ao atual → `204` sem mudança (idempotente).
5. **O último Admin ativo não pode ser rebaixado**, nem por ele mesmo. A tentativa devolve `400`. O bloqueio é o mesmo bloqueio (`UPDLOCK, HOLDLOCK`) da 2026092505.
6. **As sessões do alvo não são revogadas.** O refresh gera tokens com o perfil do banco, e as ações privilegiadas leem o banco (2026092510).
7. **Rate limit `user-role-change`:** 5 por IP a cada 15 min.
8. **O primeiro Admin continua sendo criado por SQL**, pelo runbook de `0005`. Não haverá endpoint nem variável de ambiente de bootstrap, para que não exista porta de entrada de privilégio fora do fluxo autenticado. Quem pode rodar SQL no banco já é, por definição, operador.
9. **Registro:** log `Information` com o Admin, o alvo, o perfil antigo e o novo. Evento `RoleChanged` da auditoria (2026092519).

## Tarefas

- [ ] **Dev** — Criar `User.ChangeRole(role)` no domínio e o caso de uso `ChangeUserRole` (autorização pelo banco, reautenticação com bloqueio, validações do alvo, regra do último Admin, transação)
- [ ] **Dev** — Criar o endpoint `PUT /api/users/{externalId}/role` (`[Authorize]`) e a política de rate limit `user-role-change`; atualizar a collection Postman
- [ ] **DBA** — Reaproveitar a contagem de Admins ativos com `UPDLOCK, HOLDLOCK` (2026092505) no rebaixamento; persistir `role` no update
- [ ] **Tester** — Unitários: `User` → `403` sem consultar o alvo; senha errada; alvo inexistente, excluído e não confirmado; valor fora da allowlist; valor igual (idempotente); rebaixar o último Admin ativo, inclusive a si mesmo
- [ ] **Tester** — Integração: promoção seguida de refresh → token com `role=Admin`; o Admin rebaixado perde o privilégio na hora, mesmo com token antigo (2026092510); dois rebaixamentos simultâneos dos dois últimos Admins → um falha
- [ ] **Tech Writer** — Atualizar `docs/auth/0005 - Perfis de Acesso.md`: novo endpoint, regras, e o SQL só como runbook de bootstrap do primeiro Admin
