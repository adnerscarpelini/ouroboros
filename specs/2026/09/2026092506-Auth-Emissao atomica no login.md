# 2026092506 - Emissao atomica no login

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

O usuário aprovou especificar a consistência entre revogar sessões anteriores e criar a sessão do novo login.

## Análise

O login atual revoga todos os refresh tokens ativos antes de inserir o novo. Se a inserção falhar, o usuário perde as sessões antigas e não recebe uma nova. Preservar, por enquanto, a decisão documentada de uma sessão por usuário; alterar essa regra seria uma decisão de produto separada. O login permanece público, com proteção da spec 2026092501, erro genérico e senha fora dos logs.

## Tarefas

- [ ] **Dev** — Fazer a revogação das sessões anteriores e a gravação do novo refresh token como uma única operação persistente; só devolver JWT e refresh token após confirmação da gravação.
- [ ] **DBA** — Implementar transação e serialização suficiente para dois logins concorrentes manterem a regra de uma sessão ativa.
- [ ] **Tester** — Cobrir falha ao criar token, logins simultâneos e ausência de revogação após senha inválida, com PostgreSQL real.
- [ ] **Tech Writer** — Atualizar `docs/auth/0003 - Login e Tokens.md` com a garantia transacional.
