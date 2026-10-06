# Configuração JWT

Parâmetros de emissão e validação de token do `auth-service`. Usa `appsettings.json` + Options pattern (`IOptions<JwtSettings>`), não uma tabela no banco.

Por quê:

- Os valores não precisam mudar sem deploy.
- Uma tabela colocaria o banco no caminho de toda emissão/validação de token, e ainda exigiria cache e invalidação.
- Se surgir a necessidade de ajuste em runtime (ex.: painel administrativo), isso vira uma spec própria.

## Assinatura assimétrica (RS256)

Os access tokens são assinados com **RS256** (RSA, pelo menos 2048 bits).

- A **chave privada fica só no auth-service**. Os outros serviços validam com a chave pública, publicada no JWKS, e **não conseguem emitir tokens**. Com a antiga chave compartilhada (HS256), todo serviço que validava o token também podia emitir.
- Todo JWT leva o `kid` da chave que o assinou no header.
- O auth-service valida os próprios tokens com o conjunto local de chaves públicas, sem chamada HTTP a si mesmo. Só `RS256` é aceito (`ValidAlgorithms = [RS256]`): `none` e HS256 (inclusive HS256 usando a chave pública como segredo) são rejeitados.

## Parâmetros

Seção `Jwt`, classe `Auth.Infrastructure/Security/JwtSettings.cs` (fica em Infrastructure porque é consumida pelo `JwtTokenGenerator`; a Api faz o bind e a validação):

| Chave | Padrão | Regra |
|---|---|---|
| `Issuer` | `http://localhost:8082` | Obrigatória. É a **URL pública base** do auth-service (URL absoluta `http` ou `https`). Vira o `iss` dos tokens e o `issuer` do documento de descoberta |
| `Audience` | `ouroboros` | Obrigatória |
| `AccessTokenExpirationMinutes` | `15` | 1 a 1440 |
| `RefreshTokenExpirationDays` | `7` | 1 a 365 |
| `SigningKeys` | *(vazia)* | Lista de chaves. Obrigatória, ver abaixo |

Os valores padrão ficam em `auth-service/Auth.Api/appsettings.json`.

### Chaves de assinatura (`Jwt:SigningKeys`)

Cada item tem:

| Campo | Significado |
|---|---|
| `Kid` | Identificador da chave (no header `kid` e no JWKS). Único na lista |
| `PrivateKeyPath` | Caminho de um arquivo PEM com a chave privada RSA. O PEM chega **por arquivo**, nunca por variável de ambiente nem pelo `appsettings.json` versionado |
| `Status` | `Active` ou `Published` |

- **`Active`:** a chave que assina os tokens novos. **Exatamente uma.**
- **`Published`:** só entra no JWKS e valida token. Serve à rotação, para os tokens antigos continuarem válidos.
- Chave aposentada não tem status: ela só sai da lista.

```
Jwt__SigningKeys__0__Kid=auth-2026-10
Jwt__SigningKeys__0__PrivateKeyPath=/run/keys/jwt-signing-key.pem
Jwt__SigningKeys__0__Status=Active
Jwt__SigningKeys__1__Kid=auth-2026-04
Jwt__SigningKeys__1__PrivateKeyPath=/run/keys/jwt-signing-key-old.pem
Jwt__SigningKeys__1__Status=Published
```

## Validação na subida

`ValidateOnStart` valida tudo quando a API sobe. Um valor faltando ou inválido **derruba a API** com uma mensagem clara no log. Além das `DataAnnotations`, o startup confere:

- existe **exatamente uma** chave `Active`;
- toda chave é uma RSA de **pelo menos 2048 bits**, em PEM válido, e o arquivo existe;
- os `Kid` são **únicos** e não vazios;
- o `Issuer` é uma URL `http` ou `https` absoluta.

```
OptionsValidationException: Jwt:SigningKeys must have exactly one Active key, found 2.
```

No Docker isso aparece como container em `Restarting`. Veja com `docker compose logs auth-service`.

## Endpoints públicos

Anônimos, sem política de rate limit (são leves e cacheáveis) e com `Cache-Control: public, max-age=3600`. Só devolvem dados públicos.

### `GET /.well-known/jwks.json`

As chaves `Active` e `Published`, em formato JWK (RFC 7517), **só com parâmetros públicos** (`n` e `e`; nunca `d`, `p`, `q`, `dp`, `dq`, `qi`).

```
200 OK
{ "keys": [ { "kty": "RSA", "use": "sig", "kid": "auth-2026-10", "alg": "RS256", "n": "...", "e": "AQAB" } ] }
```

### `GET /.well-known/openid-configuration`

Documento de descoberta mínimo, o suficiente para o `JwtBearer` de um consumidor funcionar só com a `Authority`:

```
200 OK
{ "issuer": "http://localhost:8082", "jwks_uri": "http://localhost:8082/.well-known/jwks.json" }
```

O auth-service **não é um provedor OpenID Connect completo**: não tem authorization endpoint nem `id_token`. Só publica o necessário para validar o access token.

## Como outro serviço valida o token

Basta a `Authority`. O consumidor busca o documento de descoberta, depois o JWKS, e escolhe a chave pelo `kid`:

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://auth.exemplo.com";   // o Jwt:Issuer do auth-service
        options.Audience = "ouroboros";                    // o Jwt:Audience
        options.TokenValidationParameters.ValidAlgorithms = ["RS256"];
        options.MapInboundClaims = false;                  // nomes curtos: sub, role, sid...
        options.TokenValidationParameters.RoleClaimType = "role";
    });
```

- O consumidor **nunca** recebe uma chave secreta: só valida.
- O JwtBearer cacheia o JWKS e o atualiza quando aparece um `kid` desconhecido. O cache de 1 h do endpoint é o que o runbook de rotação espera.
- Use `https` fora do ambiente local (`RequireHttpsMetadata` fica ligado por padrão).

## Desenvolvimento local (Docker)

1. Gere uma chave RSA de 2048 bits (a pasta `secrets/` e os arquivos `*.pem` e `*.key` estão no `.gitignore`):
   ```
   mkdir -p secrets
   openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out secrets/auth-jwt-private-key.pem
   chmod 644 secrets/auth-jwt-private-key.pem
   ```
   O container roda sem root (usuário `app`) e precisa conseguir ler o arquivo montado.
2. No `.env` da raiz:
   ```
   AUTH_JWT_KEY_ID=auth-dev-1
   AUTH_JWT_PRIVATE_KEY_FILE=./secrets/auth-jwt-private-key.pem
   ```
3. O `docker-compose.yml` monta o PEM somente leitura em `/run/keys/jwt-signing-key.pem` e repassa `Jwt__SigningKeys__0__Kid`, `PrivateKeyPath` e `Status=Active`.

Rodando fora do Docker (`dotnet run`), defina `Jwt__SigningKeys__0__Kid`, `Jwt__SigningKeys__0__PrivateKeyPath` (caminho local do PEM) e `Jwt__SigningKeys__0__Status=Active`, do mesmo jeito que `ConnectionStrings__Default`.

A chave **não** vai no `appsettings.Development.json`: o arquivo é versionado.

## Produção

- O PEM vem de um arquivo fora do git. Quando existir ambiente de produção, é um Docker secret montado e referenciado por `PrivateKeyPath` (a spec de configuração de produção está adiada nessa parte).
- Use uma chave diferente por ambiente. Quem tem a privada consegue emitir token válido.
- `Jwt__Issuer` é a URL pública do auth-service naquele ambiente.
- Os demais parâmetros podem ser sobrescritos do mesmo jeito (ex.: `Jwt__AccessTokenExpirationMinutes=5`), sem alterar o `appsettings.json`.

## Runbook de rotação de chave (sem queda)

A rotação nunca troca a chave de uma vez: os tokens já emitidos (até 15 min) e os JWKS em cache dos consumidores (1 h) precisam continuar valendo.

1. **Publicar a nova.** Gere a chave nova (`openssl genpkey ...`), monte o PEM e acrescente-a à lista como `Published`, com um `Kid` novo. A antiga continua `Active`. Reinicie o auth-service.
2. **Esperar o cache.** Aguarde **1 h**: os consumidores passam a ter a chave nova no JWKS em cache.
3. **Ativar a nova.** Troque os status: a nova vira `Active` e a antiga fica `Published`. Reinicie. Os tokens novos saem com o `kid` da nova, e os antigos continuam válidos.
4. **Aposentar a antiga.** Depois de **15 min** (vida do access token) **mais 1 h** (cache), remova a antiga da lista e reinicie. Tokens assinados por ela deixam de valer.

Em caso de vazamento da chave privada ativa, a rotação é a mesma, mas sem as esperas: o custo é derrubar os tokens vigentes (no máximo 15 min). Os refresh tokens são opacos e **continuam valendo** em qualquer rotação ou troca de emissor.

## Troca do emissor

O `Issuer` é a URL pública do auth-service. Mudar o valor (por exemplo, da antiga constante `ouroboros-auth` para a URL) invalida os access tokens emitidos com o valor antigo. Isso afeta no máximo 15 min de tokens. Os consumidores precisam configurar a mesma `Authority`.
