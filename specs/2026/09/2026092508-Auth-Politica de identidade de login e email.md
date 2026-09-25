# 2026092508 - Politica de identidade de login e email

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

O usuário aprovou especificar uma comparação consistente de login e e-mail em cadastro, busca e recuperação de conta.

## Análise

Hoje os índices e consultas comparam texto exato, de modo que a diferença de maiúsculas no domínio do e-mail pode produzir identidades duplicadas. Seguir OWASP Email Validation and Verification Cheat Sheet: preservar o endereço apresentado pelo usuário, normalizar o domínio para comparação e decidir explicitamente a política da parte local; não aplicar `lower(email)` inteiro sem avaliar colisões. O login terá forma canônica única em cadastro, autenticação e busca. Antes da migration, detectar e resolver colisões legadas sem unir contas automaticamente. Preservar respostas genéricas para e-mail e o ownership nas buscas.

## Tarefas

- [ ] **Dev** — Centralizar a canonicalização de login e e-mail e aplicá-la a cadastro, login, busca e solicitação de reset, mantendo o valor original de e-mail para exibição e entrega.
- [ ] **DBA** — Auditar colisões atuais, definir estratégia de resolução manual e criar índices/colunas de comparação coerentes com a política, com migration segura para dados existentes.
- [ ] **Tester** — Cobrir diferenças de caixa e Unicode conforme a política, colisões de cadastro, busca e reset sem revelar a existência da conta.
- [ ] **Tech Writer** — Documentar a política de comparação, as decisões sobre a parte local do e-mail e a migração em `docs/auth/`.
