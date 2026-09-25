# 2026092512 - Testes de integracao da API e do banco

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

O usuário aprovou especificar testes que validem SQL, migrations, endpoints e autorização reais, além dos testes unitários existentes.

## Análise

Os testes atuais de Domain e Application usam repositórios falsos; não demonstram o comportamento do PostgreSQL, das transações, do middleware JWT nem do rate limiting. Criar uma suíte com PostgreSQL descartável e servidor HTTP de teste, sem dependência de banco compartilhado ou de segredos reais. A suíte deve ser adicionada à solution e executada pela CI da spec 2026092513. Cobrir em primeiro lugar os riscos das specs 2026092501–2026092510.

## Tarefas

- [ ] **Dev** — Preparar inicialização testável da API e migrations no ambiente de integração, preservando isolamento entre testes.
- [ ] **Tester** — Criar testes HTTP e de persistência com PostgreSQL descartável para cadastro, confirmação, login, refresh, reset, exclusão, autorização, rate limiting e concorrência.
- [ ] **Tester** — Verificar rollback e conflitos de unicidade com falhas controladas, além de respostas que não revelam e-mail existente.
- [ ] **Tech Writer** — Registrar comandos e pré-requisitos para executar a suíte localmente em `docs/project/`.
