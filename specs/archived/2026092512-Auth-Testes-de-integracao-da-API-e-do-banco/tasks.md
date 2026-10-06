# 2026092512 - Testes de integracao da API e do banco — Tarefas

- [x] **Dev** — Expor `public partial class Program` e garantir que a configuração injetada pelo teste (connection string, chave, limites) valha já nas migrations do startup
- [x] **Tester** — Criar o projeto `Auth.Integration.Tests` com a fixture Testcontainers + `WebApplicationFactory`, a limpeza entre testes e o decorator de falha controlada; incluí-lo na solution
- [x] **Tester** — Cobrir a linha de base pela API: cadastro (inclusive a resposta genérica para e-mail ocupado), confirmação, login, refresh com rotação, logout, reset, consulta com regras de perfil e dono, exclusão, `401` sem token, `403` por perfil e `429` nas políticas atuais
- [x] **Tester** — Cobrir o SQL dos repositórios que os testes unitários não alcançam: filtros de `deleted_at`, índice parcial de e-mail, `ON DELETE CASCADE` e escritas condicionais (`TryRevokeAsync`, `TryMarkAsUsedAsync`)
- [x] **Tech Writer** — Criar doc em `docs/project/` com pré-requisitos (Docker), o comando para rodar a suíte e como escrever um teste novo
