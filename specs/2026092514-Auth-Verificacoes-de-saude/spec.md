# 2026092514 - Verificacoes de saude

**Data:** 25/09/2026
**Servico(s):** auth-service

## Solicitação

Na validação de maturidade do auth-service, faltavam endpoints de saúde para um orquestrador saber se a API está viva e pronta. O usuário aprovou criá-los.

## Análise

Extensão do `auth-service`. Referências: Health Checks do ASP.NET Core e as sondas liveness/readiness do Kubernetes.

Decisões:
1. **`GET /health/live`** não depende de nada: só confirma que o processo responde. **`GET /health/ready`** confere o SQL Server com um `SELECT 1` de timeout de 2 s, via `AddHealthChecks`.
2. **A resposta traz só o status:** `Healthy` com `200` ou `Unhealthy` com `503`. Nada de mensagem de exceção, connection string ou nome de host.
3. **Os endpoints ficam:**
   - anônimos;
   - fora do rate limit;
   - fora do log de requisições do Serilog em `Information`, porque as sondas batem a cada poucos segundos e poluiriam o log. Só as falhas são logadas.
4. **A exposição externa é bloqueada no proxy de borda** (2026092511). O acesso fica restrito à rede interna.
5. **Sem `healthcheck` do auth-service no Compose por enquanto.** A imagem `aspnet` não tem `curl`, instalá-lo aumenta a superfície de ataque, e nenhum serviço do Compose depende do auth-service ainda. O orquestrador sonda por HTTP direto. Quando surgir um serviço dependente, isso é reavaliado.
6. **O envio de e-mail fica fora da readiness** enquanto ele não existir.
