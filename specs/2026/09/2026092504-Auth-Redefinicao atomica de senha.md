# 2026092504 - Redefinicao atomica de senha

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

O usuário aprovou especificar uma redefinição de senha que conclua ou reverta integralmente as mudanças de token, senha e sessão.

## Análise

Hoje o token de reset é marcado como usado antes de gravar a nova senha e revogar os refresh tokens. Falha intermediária consome o link sem trocar a senha. Preservar a spec 2026092302: token de uso único, usuário ativo, senha diferente, sem login automático e resposta genérica para token inválido. A política de senha atualizada virá da spec 2026092509; a decisão de validade imediata do JWT pertence à spec 2026092510.

## Tarefas

- [ ] **Dev** — Coordenar consumo do token, troca da senha e revogação dos refresh tokens como operação indivisível; falha de validação de senha não consome o token.
- [ ] **DBA** — Implementar transação e consumo condicional com `used_at IS NULL` e validade verificada no banco, sem expor a transação às camadas internas.
- [ ] **Tester** — Cobrir disputa pelo mesmo token, falhas em cada escrita e rollback com PostgreSQL real; confirmar que só uma requisição troca a senha.
- [ ] **Tech Writer** — Atualizar `docs/auth/0004 - Recuperacao de Senha.md`, removendo a limitação documentada de escritas separadas.
