# 2026092503 - Confirmacao atomica de cadastro

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

O usuário aprovou especificar o uso único e a consistência da confirmação do e-mail de cadastro.

## Análise

Hoje `ConfirmEmailInteractor` atualiza o usuário e depois o token, sem transação nem consumo condicional no banco. Duas requisições podem ler o mesmo token pendente e ambas concluir. Seguir a regra já adotada para reset: validar tipo e prazo do token, consumi-lo uma única vez e ativar a conta na mesma transação. O endpoint permanece público por necessidade do fluxo; token, conta e e-mail não entram em URL nem logs.

## Tarefas

- [ ] **Dev** — Fazer confirmação e consumo do token indivisíveis e devolver erro genérico para token inválido, expirado ou já utilizado.
- [ ] **DBA** — Adicionar consumo condicional do token com verificação de validade no próprio comando SQL e atualização do usuário na mesma transação.
- [ ] **Tester** — Testar duas confirmações simultâneas, expiração durante a operação e rollback quando a atualização do usuário falhar, usando PostgreSQL real.
- [ ] **Tech Writer** — Atualizar `docs/auth/0001 - Confirmacao de Cadastro.md` para refletir a garantia de uso único.
