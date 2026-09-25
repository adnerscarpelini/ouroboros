# 2026092513 - Integracao continua

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

O usuário aprovou especificar uma verificação automática de build e testes para mudanças no auth-service.

## Análise

Não há pipeline de CI versionado no repositório. Criar workflow que restaure, compile e execute testes da solution; após a spec 2026092512, incluir também os testes com PostgreSQL descartável. O pipeline não deve receber segredos de produção nem publicar artefatos automaticamente. Falhas devem impedir que o resultado seja considerado aprovado.

## Tarefas

- [ ] **Dev** — Criar pipeline de CI com SDK .NET compatível, cache seguro de dependências e execução de build e testes da solution.
- [ ] **Tester** — Integrar a suíte de testes de integração, com dependências descartáveis e resultado legível em caso de falha.
- [ ] **Tech Writer** — Documentar o gate da CI e como reproduzir seus comandos localmente.
