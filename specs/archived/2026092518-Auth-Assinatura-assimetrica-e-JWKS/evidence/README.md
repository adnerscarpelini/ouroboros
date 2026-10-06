# Evidências

## Execução

- Comando: `dotnet test Ouroboros.slnx`
- Data: 06/10/2026
- Resultado: 519 testes, 519 aprovados, 0 falhas. Saída bruta em `dotnet-test.txt`.

| Projeto | Total | Passou | Falhou |
|---|---|---|---|
| Auth.Domain.Tests | 61 | 61 | 0 |
| Auth.Application.Tests | 189 | 189 | 0 |
| Auth.Integration.Tests | 269 | 269 | 0 |

Antes desta spec eram 484 testes (61 + 189 + 234). Os testes de integração usam um SQL Server real em container (Testcontainers) e chaves RSA geradas a cada execução (`TestSigningKeys`).

## Rastreabilidade

Caminhos relativos a `test/auth-service/`.

### Decisões da spec

| Decisão | Teste | Resultado |
|---|---|---|
| 1. RS256 com RSA de pelo menos 2048 bits; privada só no auth-service | `Auth.Integration.Tests/Security/SigningKeyStoreTests.cs::ShouldIssueRs256TokenWithTheKidOfTheActiveKeyAndTheSessionId`, `::ShouldFailWithAnRsaKeySmallerThan2048Bits`, `::ShouldFailWhenTheActiveKeyIsSmallerThan2048Bits`; `Api/JwksApiTests.cs::ShouldIssueRs256TokensWithTheKidOfTheActiveKeyAndValidateThem`, `::ShouldPublishRealPublicKeysThatVerifyTheTokens` (um consumidor valida com as chaves do JWKS, sem nenhuma chave secreta) | Passou |
| 2. `kid` no header de todo JWT | `SigningKeyStoreTests::ShouldIssueRs256TokenWithTheKidOfTheActiveKeyAndTheSessionId`; `JwksApiTests::ShouldIssueRs256TokensWithTheKidOfTheActiveKeyAndValidateThem`, `::ShouldKeepRefreshTokensValidAfterTheSwitchBecauseTheyAreOpaque` | Passou |
| 3. `GET /.well-known/jwks.json`: chaves `Active` e `Published`, nunca parâmetros privados | `JwksApiTests::ShouldPublishTheActiveAndThePublishedKeysWithoutPrivateParameters`, `::ShouldNeverExposePrivateParametersInTheJwks`; `SigningKeyStoreTests::ShouldNeverCarryPrivateParametersInTheJsonWebKeys` | Passou |
| 3. `GET /.well-known/openid-configuration` mínimo (`issuer` e `jwks_uri`) | `JwksApiTests::ShouldReturnAMinimalDiscoveryDocumentWithIssuerAndJwksUri` | Passou |
| 3. Anônimos, `Cache-Control: public, max-age=3600`, sem política de rate limit | `JwksApiTests::ShouldBePublicCacheableAndOutsideTheRateLimit` (2 endpoints, 12 chamadas do mesmo IP com limite de teste de 3) | Passou |
| 3. Consumidor configurado só com a `Authority` valida o token pela descoberta | `JwksApiTests::ShouldLetAConsumerConfiguredOnlyWithTheAuthorityValidateTheTokenThroughDiscovery` (`ConfigurationManager<OpenIdConnectConfiguration>` busca a descoberta e o JWKS, como o JwtBearer faz) | Passou |
| 4. `issuer` vira a URL pública base; tokens com o issuer antigo deixam de valer; refresh tokens continuam | `JwksApiTests::ShouldRejectATokenFromTheOldIssuerEvenWhenItIsSignedWithTheActiveKey`, `::ShouldKeepRefreshTokensValidAfterTheSwitchBecauseTheyAreOpaque`; `Api/JwtKeyRotationApiTests.cs::ShouldFailAtStartupWhenTheIssuerIsNotAPublicHttpUrl` (2 casos) | Passou |
| 5. `Jwt:SigningKeys`: startup exige exatamente uma `Active`, RSA de 2048 bits ou mais e `kid` únicos | `SigningKeyStoreTests::ShouldAcceptOneActiveKeyAndPublishedKeys`, `::ShouldFailWithoutAnyKey`, `::ShouldFailWithoutAnActiveKey`, `::ShouldFailWithTwoActiveKeys`, `::ShouldFailWithARepeatedKid`, `::ShouldFailWhenTheKidIsMissing`, `::ShouldFailWhenThePemFileDoesNotExist`, `::ShouldFailWhenTheFileIsNotAnRsaPrivateKey`; com a API de verdade: `JwtKeyRotationApiTests::ShouldFailAtStartupWithoutAnyKey`, `::ShouldFailAtStartupWithoutAnActiveKey`, `::ShouldFailAtStartupWithTwoActiveKeys`, `::ShouldFailAtStartupWithAKeySmallerThan2048Bits`, `::ShouldFailAtStartupWithARepeatedKid` | Passou |
| 5. O JWKS publica `Active` e `Published` | `JwksApiTests::ShouldPublishTheActiveAndThePublishedKeysWithoutPrivateParameters`; `SigningKeyStoreTests::ShouldSignWithTheActiveKeyAndPublishTheActiveAndThePublishedOnes` | Passou |
| 6. Rotação sem queda (publicar, esperar, ativar, aposentar) | `JwtKeyRotationApiTests::ShouldRotateTheKeyWithoutDroppingAnyValidToken` (quatro fases, cada uma com a API configurada como o runbook manda; o token antigo vale até a antiga sair da lista) | Passou |
| 7. O auth-service valida os próprios tokens com as chaves locais, `ValidAlgorithms = [RS256]` | `JwksApiTests::ShouldRejectATokenSignedWithHs256UsingThePublicKeyAsSecret`, `::ShouldRejectATokenSignedWithTheOldSharedHmacKey`; `Api/UserQueryApiTests.cs::ShouldReturnUnauthorizedWhenTokenUsesAlgorithmNone`, `::ShouldReturnUnauthorizedWhenTokenIsSignedWithAnotherKey` (mesmo `kid`, outra chave RSA), `::ShouldReturnUnauthorizedWhenTokenIsExpired`; `JwksApiTests::ShouldAcceptATokenSignedWithThePublishedKeyAndRejectOneWithAnUnknownKid` | Passou |

### Tarefa Dev

| Critério | Teste | Resultado |
|---|---|---|
| `Jwt:SigningKeys` com validação no startup; assinatura RS256 com `kid` no `JwtTokenGenerator` | `SigningKeyStoreTests`, `JwtKeyRotationApiTests` | Passou |
| `JwtBearer` com o conjunto local de chaves públicas, `ValidAlgorithms = [RS256]`, `issuer` pela URL pública | `JwksApiTests`, `UserQueryApiTests` | Passou |
| Endpoints `jwks.json` e `openid-configuration`; collection Postman | `JwksApiTests`; pasta "Well-Known" em `auth-service/Auth.Api/Postman/Auth.postman_collection.json` | Passou |
| `.env.example`, Compose de dev (montagem do PEM) e `.gitignore` (`secrets/`, `*.pem`, `*.key`) | `docker compose --env-file .env.example config -q` sem erro; imagem construída e executada com o PEM montado somente leitura: `GET /.well-known/jwks.json` devolveu `200`, `Cache-Control: public, max-age=3600` e a chave `auth-dev-1`, e `GET /health/live` devolveu `Healthy`, rodando como o usuário `app` (sem root) | Passou (manual) |

### Tarefa Tester

| Critério | Teste | Resultado |
|---|---|---|
| Unitários: token com `kid` da chave ativa; startup falha sem chave ativa, com duas ativas, com chave menor que 2048 ou com `kid` repetido | `SigningKeyStoreTests` | Passou |
| Integração: token validado; HS256 e `alg: none` rejeitados; o JWKS lista `Active` e `Published` e nunca expõe privados; consumidor só com `Authority` valida pelo discovery | `JwksApiTests`, `UserQueryApiTests` | Passou |

### Tarefa Tech Writer

| Critério | Artefato | Resultado |
|---|---|---|
| RS256, chaves, geração local, runbook de rotação e como outro serviço configura a validação | `docs/auth/0002 - Configuracao JWT.md` (reescrita); ajustes em `docs/auth/0003 - Login e Tokens.md` e `docs/project/0004 - Testes de Integracao.md` | Entregue |

## Observações

- **Chaves dos testes.** O `AuthApiFactory` deixou de usar uma chave HMAC fixa. `TestSigningKeys` gera duas chaves RSA de 2048 bits por execução, grava os PEM numa pasta temporária e a API os lê por caminho, como em produção. A `AuthApiFactory` aceita uma lista de chaves própria, usada nos testes de rotação e de validação do startup.
- **Validador fora do Infrastructure.** `JwtSettingsValidator` (a `IValidateOptions<JwtSettings>` que confere o issuer e as chaves) fica em `Auth.Api/Configuration/`, porque o Infrastructure não referencia `Microsoft.Extensions.Options`. A regra das chaves em si (`SigningKeyStore.Validate`) está no Infrastructure e é a que os testes unitários exercitam.
- **Consumidor com `Authority`.** O teste usa o mesmo mecanismo do `JwtBearer` (`ConfigurationManager<OpenIdConnectConfiguration>`), com um recuperador de documentos que reescreve o host público (`http://localhost:8082`) para o `TestServer`. Não subi um segundo host só para isso.
- **`.env` local.** O `.env` de quem roda o projeto ainda tem `AUTH_JWT_SIGNING_KEY` (obsoleta) e precisa de `AUTH_JWT_KEY_ID` e `AUTH_JWT_PRIVATE_KEY_FILE` e do PEM gerado. Não alterei o `.env`, que não é versionado. O passo a passo está na doc 0002.
- **Fora do escopo.** Os Docker secrets de produção continuam adiados (spec 2026092511). O PEM chega por caminho de arquivo, o que não depende deles.
