# 2026092518 - Assinatura assimetrica e JWKS — Tarefas

- [x] **Dev** — Substituir `SigningKey` por `Jwt:SigningKeys` (com validação no startup) e assinar com RS256 incluindo o `kid` no `JwtTokenGenerator`
- [x] **Dev** — Ajustar a validação do `JwtBearer` para o conjunto local de chaves públicas e `ValidAlgorithms = [RS256]`; trocar o `issuer` pela URL pública base
- [x] **Dev** — Criar `GET /.well-known/jwks.json` e `GET /.well-known/openid-configuration`, anônimos e com `Cache-Control`; atualizar a collection Postman
- [x] **Dev** — Atualizar `.env.example`, o Compose de dev (montagem do PEM) e o `.gitignore` (arquivos de chave)
- [x] **Tester** — Unitários: token com `kid` da chave ativa; o startup falha sem chave ativa, com duas ativas, com chave menor que 2048 ou com `kid` repetido
- [x] **Tester** — Integração: token validado; token HS256 ou com `alg: none` rejeitado; o JWKS lista `Active` e `Published` e nunca expõe parâmetros privados; um consumidor configurado só com `Authority` valida o token pelo discovery
- [x] **Tech Writer** — Atualizar `docs/auth/0002 - Configuracao JWT.md` com RS256, as chaves, a geração local (`openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048`), o runbook de rotação e como outro serviço configura a validação
