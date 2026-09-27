# 2026092512 - Testes de integracao da API e do banco

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

Na validação de maturidade do auth-service, os 119 testes cobrem só Domain e Application, com repositórios falsos. Nada testa o SQL, as migrations, o middleware JWT, o rate limiting nem as transações. O usuário aprovou uma suíte de integração com ferramenta definida, e ela é a **primeira** spec a implementar, porque as de transação dependem dela.

## Análise

Novo projeto de teste do `auth-service`. Referências: integration tests do ASP.NET Core (`WebApplicationFactory`) e Testcontainers para .NET.

Decisões:
1. **Projeto `test/auth-service/Auth.Integration.Tests`**, com xUnit como os atuais, incluído na solution.
2. **PostgreSQL real e descartável** via `Testcontainers.PostgreSql`, com a imagem `postgres:16-alpine`, a mesma do Compose. Um container por execução (collection fixture), com migrations aplicadas pelo `MigrationRunner`.
3. **API em memória** via `WebApplicationFactory<Program>` (`Microsoft.AspNetCore.Mvc.Testing`), com `public partial class Program` exposto. A configuração de teste injeta a connection string do container, uma chave de assinatura de teste e limites de rate limit ajustáveis (2026092501).
4. **A connection string do teste precisa valer já no startup.** O `Program.cs` roda as migrations antes do `builder.Build()`. A configuração de teste é injetada com `UseSetting` para ser vista nesse ponto. Quando a 2026092511 tirar as migrations do startup, isso fica mais simples.
5. **Isolamento.** O banco é zerado entre testes com `TRUNCATE ... RESTART IDENTITY CASCADE` das tabelas do schema `auth`, exceto o journal do DbUp. Os testes da mesma collection não rodam em paralelo.
6. **Falhas controladas** para testar rollback: um decorator de repositório que lança exceção numa chamada escolhida, registrado com `ConfigureTestServices`.
7. **Primeiro a linha de base.** Antes das specs de mudança, cobrir os fluxos que já existem: cadastro, confirmação, login, refresh, logout, reset, consulta, exclusão, autorização por perfil e os rate limits atuais. Cada spec seguinte acrescenta aqui os próprios testes.
8. **Sem banco compartilhado nem segredos reais.** O único requisito é Docker, na máquina local e na CI (2026092513).

## Tarefas

- [x] **Dev** — Expor `public partial class Program` e garantir que a configuração injetada pelo teste (connection string, chave, limites) valha já nas migrations do startup
- [ ] **Tester** — Criar o projeto `Auth.Integration.Tests` com a fixture Testcontainers + `WebApplicationFactory`, a limpeza entre testes e o decorator de falha controlada; incluí-lo na solution
- [ ] **Tester** — Cobrir a linha de base pela API: cadastro (inclusive a resposta genérica para e-mail ocupado), confirmação, login, refresh com rotação, logout, reset, consulta com regras de perfil e dono, exclusão, `401` sem token, `403` por perfil e `429` nas políticas atuais
- [ ] **Tester** — Cobrir o SQL dos repositórios que os testes unitários não alcançam: filtros de `deleted_at`, índice parcial de e-mail, `ON DELETE CASCADE` e escritas condicionais (`TryRevokeAsync`, `TryMarkAsUsedAsync`)
- [ ] **Tech Writer** — Criar doc em `docs/project/` com pré-requisitos (Docker), o comando para rodar a suíte e como escrever um teste novo
