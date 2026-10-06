# 2026092514 - Verificacoes de saude — Tarefas

- [x] **Dev** — Registrar os health checks com a verificação do SQL Server e mapear `/health/live` (sem checks) e `/health/ready` (com o banco), anônimos, fora do rate limit e com resposta só de status
- [x] **Dev** — Filtrar as requisições de health do `UseSerilogRequestLogging` quando bem-sucedidas
- [x] **Tester** — Integração: `live` e `ready` com `200` e o banco no ar; `ready` com `503` e `live` com `200` com o banco parado; o corpo não contém detalhes internos
- [x] **Tech Writer** — Criar doc em `docs/project/` com a semântica de cada endpoint, o uso em sondas de orquestrador e a restrição de acesso externo
