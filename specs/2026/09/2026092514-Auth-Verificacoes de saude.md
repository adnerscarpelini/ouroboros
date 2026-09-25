# 2026092514 - Verificacoes de saude

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

O usuário aprovou especificar verificações de saúde para operação do auth-service.

## Análise

O Compose verifica o PostgreSQL, mas não a disponibilidade e prontidão da própria API. Criar liveness independente do banco e readiness que confirme dependências necessárias sem expor credenciais, detalhes internos ou dados de usuários. Endpoints de saúde podem ser públicos para sondas internas, mas a exposição externa deve ser restrita na configuração de produção da spec 2026092511. Não depender da futura integração de e-mail para declarar prontidão enquanto ela não existir.

## Tarefas

- [ ] **Dev** — Expor `/health/live` e `/health/ready`, configurar a verificação do PostgreSQL e conectar a readiness ao orquestrador/Compose quando aplicável.
- [ ] **Tester** — Cobrir API iniciada, banco indisponível e respostas sem informação sensível.
- [ ] **Tech Writer** — Documentar semântica, acesso e uso dos endpoints em `docs/project/`.
