# Verificações de Saúde

O auth-service expõe dois endpoints para um orquestrador saber se a API está viva e se está pronta. Usam os Health Checks do ASP.NET Core.

## Endpoints

| Endpoint | O que confere | Resposta |
|---|---|---|
| `GET /health/live` | Nada: só que o processo responde | `200 Healthy` |
| `GET /health/ready` | O SQL Server, com um `SELECT 1` de timeout de 2 s | `200 Healthy` ou `503 Unhealthy` |

```
GET /health/ready

200 OK
Healthy
```

- **A resposta traz só o status**, em texto: `Healthy` ou `Unhealthy`. Nunca a mensagem de exceção, a connection string nem o nome do host. O detalhe da falha vai só para o log do serviço (`Error`, `SQL Server readiness check failed`).
- **Anônimos.** Não pedem token.
- **Fora do rate limit.** Só as políticas nomeadas limitam (ver `docs/auth/0004 - Recuperacao de Senha.md`, seção "Limite de requisições"), e as sondas não usam nenhuma.
- **Fora do log de requisições em `Information`.** As sondas batem a cada poucos segundos e poluiriam o log. O `UseSerilogRequestLogging` rebaixa para `Verbose` as respostas bem-sucedidas de `/health/live` e `/health/ready`. **Só as falhas são logadas**: um `503` (ou uma exceção) sai em `Error`, como qualquer requisição que falha.
- O envio de e-mail ficará **fora** da readiness enquanto ele não existir.

## Uso em sondas de orquestrador

- **Liveness (`/health/live`):** reiniciar o container se falhar. Não depende de banco, então uma queda do SQL Server não reinicia a API em loop.
- **Readiness (`/health/ready`):** tirar a instância do balanceamento enquanto responder `503`, sem reiniciar. Volta sozinha quando o banco volta.
- Exemplo de sonda HTTP do Kubernetes:
  ```yaml
  livenessProbe:
    httpGet: { path: /health/live, port: 8082 }
  readinessProbe:
    httpGet: { path: /health/ready, port: 8082 }
    periodSeconds: 10
  ```

## Restrição de acesso externo

Os endpoints são para a rede interna. **A exposição externa deve ser bloqueada no proxy de borda** (por exemplo, não repassar `/health/*` para fora), que só vai existir com o ambiente de produção (spec 2026092511, adiada nessa parte). Por ora:

- a porta da API só é publicada no Compose de desenvolvimento (`docker-compose.yml`);
- esta restrição fica registrada aqui como requisito do proxy.

## Sem `healthcheck` no Compose

O serviço `auth-service` não tem `healthcheck` no `docker-compose.yml` por enquanto: a imagem `aspnet` não tem `curl`, e instalá-lo aumentaria a superfície de ataque. Nenhum serviço do Compose depende do auth-service ainda, e o orquestrador sonda por HTTP direto. Quando surgir um serviço dependente, isso é reavaliado.

## Onde está no código

- Registro, rotas e nível de log: `Auth.Api/Health/HealthConfiguration.cs`.
- Verificação do banco: `Auth.Api/Health/SqlServerHealthCheck.cs`.
- Ligação: `Auth.Api/Program.cs` (`AddServiceHealthChecks`, `MapServiceHealthChecks` e `UseSerilogRequestLogging`).
- Collection Postman: pasta "Health" em `auth-service/Auth.Api/Postman/Auth.postman_collection.json`.
