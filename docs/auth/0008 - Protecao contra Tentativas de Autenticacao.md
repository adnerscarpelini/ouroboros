# Proteção contra Tentativas de Autenticação

## Limites por IP

Cada política usa uma janela fixa de 15 minutos por IP. O excesso responde `429`.

As políticas são registradas em `auth-service/Auth.Api/Configuration/RateLimitingConfiguration.cs`. Para alterar os limites ou a duração da janela, edite a seção `RateLimiting` em `auth-service/Auth.Api/appsettings.json`.

| Endpoint | Política | Tentativas por IP |
|---|---|---:|
| `POST /api/auth/login` | `auth-login` | 20 |
| `POST /api/auth/refresh` | `auth-refresh` | 60 |
| `POST /api/users/confirm-email` | `email-confirm` | 10 |
| `POST /api/users` | `user-register` | 5 |
| `POST /api/users/password-reset/request` | `password-reset-request` | 5 |
| `POST /api/users/password-reset/confirm` | `password-reset-confirm` | 10 |
| `DELETE /api/users/{externalId}` | `user-delete` | 5 |

As chaves são `RateLimiting:{nome-da-politica}:PermitLimit` e `RateLimiting:{nome-da-politica}:Window`. O tempo usa o formato `hh:mm:ss`. Valores não positivos impedem a inicialização. Os contadores ficam na memória de cada instância da API; com mais de uma réplica, será necessário um armazenamento compartilhado.

## Bloqueio por conta

- Cinco falhas consecutivas de senha bloqueiam a conta por 15 minutos e zeram o contador.
- Uma senha correta zera o contador. O bloqueio expira automaticamente.
- A regra vale no login e na senha exigida para excluir uma conta. As futuras reautenticações para troca de senha e perfil deverão usar o mesmo registro de falhas.
- Login inexistente, senha errada e conta bloqueada respondem `401 Invalid login or password`.
- Cada tentativa de login executa uma verificação de hash. Para conta inexistente ou bloqueada, usa um hash fictício; a senha real de conta bloqueada não é verificada.
- `access_failed_count` e `lockout_end` ficam em `auth.users`. O incremento acontece em um `UPDATE` atômico, para preservar falhas concorrentes entre instâncias.

Falhas de login são registradas em `Warning` com o login tentado e o IP, sem senha nem token. O bloqueio registra o `externalId` da conta.

## Proxy confiável

Configure `ForwardedHeaders:KnownProxies` com IPs e `ForwardedHeaders:KnownNetworks` com faixas CIDR dos proxies imediatamente à frente da API. Exemplo:

```json
"ForwardedHeaders": {
  "KnownProxies": ["10.0.0.10"],
  "KnownNetworks": ["10.1.0.0/16"]
}
```

A API aceita `X-Forwarded-For` e `X-Forwarded-Proto` apenas dessas origens e lê no máximo um salto. Com ambas as listas vazias, usa somente o IP da conexão. O middleware de encaminhamento roda antes do limitador por IP.
