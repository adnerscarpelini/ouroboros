# Configuração JWT

Parâmetros de emissão de token do `auth-service`. Usa `appsettings.json` + Options pattern (`IOptions<JwtSettings>`), não uma tabela no banco.

Por quê:

- Os valores não precisam mudar sem deploy.
- Uma tabela colocaria o banco no caminho de toda emissão/validação de token, e ainda exigiria cache e invalidação.
- Se surgir a necessidade de ajuste em runtime (ex.: painel administrativo), isso vira uma spec própria.

## Parâmetros

Seção `Jwt`, classe `Auth.Api/Configuration/JwtSettings.cs`:

| Chave | Padrão | Regra |
|---|---|---|
| `SigningKey` | *(vazio)* | Obrigatória, mínimo 32 caracteres (HMAC-SHA256). **Nunca versionada.** |
| `Issuer` | `ouroboros-auth` | Obrigatória |
| `Audience` | `ouroboros` | Obrigatória |
| `AccessTokenExpirationMinutes` | `15` | 1 a 1440 |
| `RefreshTokenExpirationDays` | `7` | 1 a 365 |

Os valores padrão ficam em `auth-service/Auth.Api/appsettings.json`.

## Validação na subida

`ValidateOnStart` valida tudo quando a API sobe. Um valor faltando ou inválido **derruba a API** com uma mensagem clara no log:

```
OptionsValidationException: DataAnnotation validation failed for 'JwtSettings' members: 'SigningKey' with the error: 'The SigningKey field is required.'
```

No Docker isso aparece como container em `Restarting`. Veja com `docker compose logs auth-service`.

## Desenvolvimento local (Docker)

1. Gere uma chave:
   ```
   openssl rand -base64 48
   ```
2. Coloque-a no `.env` da raiz:
   ```
   AUTH_JWT_SIGNING_KEY=<chave gerada>
   ```
3. O `docker-compose.yml` repassa para o container como `Jwt__SigningKey`.

Rodando fora do Docker (`dotnet run`), defina a variável de ambiente `Jwt__SigningKey` no shell, do mesmo jeito que `ConnectionStrings__Default`.

A chave **não** vai no `appsettings.Development.json`: o arquivo é versionado.

## Produção

- `Jwt__SigningKey` vem de variável de ambiente ou do cofre de segredos do ambiente, nunca de arquivo versionado.
- Use uma chave diferente por ambiente. Quem tem a chave consegue emitir token válido.
- Trocar a chave invalida todos os tokens já emitidos.
- Os demais parâmetros podem ser sobrescritos do mesmo jeito (ex.: `Jwt__AccessTokenExpirationMinutes=5`), sem alterar o `appsettings.json`.
