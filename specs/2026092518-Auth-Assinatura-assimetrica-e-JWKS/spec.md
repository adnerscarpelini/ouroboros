# 2026092518 - Assinatura assimetrica e JWKS

**Data:** 25/09/2026
**Servico(s):** auth-service

## Solicitação

Na validação de maturidade do auth-service, o ponto de arquitetura mais relevante foi a assinatura HS256 com chave compartilhada. Todo microsserviço que valida o token precisaria da mesma chave secreta e, com ela, também conseguiria emitir tokens. O usuário aprovou trocar por assinatura assimétrica com publicação das chaves públicas.

## Análise

Extensão do `auth-service`. Depende de 2026092511 (segredos via key-per-file). Referências:
- RFC 7517 (JWK) e RFC 7518 (JWA);
- OpenID Connect Discovery (`/.well-known/openid-configuration`);
- o modelo de Entra ID, Keycloak e Auth0, em que os consumidores configuram só a `Authority`.

Decisões:
1. **RS256 com RSA de pelo menos 2048 bits.** O OpenID Connect exige suporte a RS256, e todo validador de mercado aceita. A chave privada fica só no auth-service. Os outros serviços validam com a chave pública e não conseguem emitir tokens.
2. **`kid` no header de todo JWT**, para identificar a chave que assinou.
3. **Endpoints públicos (anônimos), só com dados públicos:**
   - `GET /.well-known/jwks.json` devolve as chaves públicas em formato JWK;
   - `GET /.well-known/openid-configuration` devolve um documento mínimo de descoberta, com `issuer` e `jwks_uri`, o suficiente para o `JwtBearer` dos consumidores funcionar só com `Authority`. O auth-service **não** é um provedor OpenID Connect completo (não tem authorization endpoint nem id_token), e a doc diz isso;
   - `Cache-Control: public, max-age=3600` nos dois, sem política de rate limit própria, porque são leves e cacheáveis.
4. **`issuer` vira a URL pública base do auth-service** (em dev, `http://localhost:8082`), o que a descoberta exige. Os tokens emitidos com o issuer antigo (`ouroboros-auth`) deixam de valer na troca. Isso afeta no máximo 15 min de tokens e é aceitável. Os refresh tokens são opacos e continuam valendo.
5. **Chaves em `Jwt:SigningKeys`**, uma lista de `{ kid, privateKeyPath, status }` com status `Active`, `Published` ou `Retired`.
   - O PEM chega por arquivo: Docker secret (2026092511) em produção e arquivo local fora do git em dev.
   - O startup valida que existe exatamente uma chave `Active`, que toda chave RSA tem pelo menos 2048 bits e que os `kid` são únicos.
   - O JWKS publica as chaves `Active` e `Published`.
6. **Rotação sem queda:**
   1. publicar a chave nova como `Published`;
   2. esperar o cache (1 h);
   3. tornar a nova `Active` e deixar a antiga como `Published`;
   4. depois de 15 min mais o cache, marcar a antiga como `Retired`.

   O passo a passo vira runbook.
7. **O auth-service valida os próprios tokens** com o conjunto local de chaves, sem chamada HTTP a si mesmo. `ValidAlgorithms = [RS256]`, e issuer e audience continuam validados como hoje. A `SigningKey` HMAC sai da configuração.
