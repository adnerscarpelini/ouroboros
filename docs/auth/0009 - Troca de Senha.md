# Troca de Senha

Troca de senha de quem já está logado. O caminho "esqueci a senha", por e-mail, está em `docs/auth/0004 - Recuperacao de Senha.md`.

## Endpoint

```
PUT /api/users/me/password
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
{ "currentPassword": "Str0ng-Passphrase-1", "newPassword": "cavalo bateria grampo cedilha" }

204 No Content
```

- `[Authorize]`: exige o access token. O `me` é o `sub` do token.
- **Não existe variante para outra conta.** Um Admin não troca a senha de ninguém (menor privilégio). `PUT /api/users/{externalId}/password` responde `404`. A recuperação de conta alheia continua sendo o reset por e-mail.
- A resposta não traz corpo.

## Erros

| Situação | Status | Mensagem |
|---|---|---|
| Sem token, token inválido ou expirado | `401` | sem corpo |
| Usuário do token inexistente, excluído ou inativo | `401` | `Invalid access token` |
| Senha atual errada, vazia ou conta bloqueada | `401` | `Invalid login or password` |
| Nova senha fora da política (tamanho, lista local, palavras do contexto) | `400` | mensagem da regra violada |
| Nova senha em vazamento conhecido | `400` | `Password is too common or has appeared in a data breach` |
| Nova senha igual à atual | `400` | `New password must be different from the current password` |
| Limite de requisições excedido | `429` | vazio |

## Reautenticação

- A troca pede a senha atual (OWASP Authentication Cheat Sheet): um access token roubado sozinho não troca a credencial.
- Senha errada conta para o bloqueio de conta (5 falhas, 15 min; ver `docs/auth/0008 - Protecao contra Tentativas de Autenticacao.md`). A falha é gravada em autocommit, fora da transação.
- Conta bloqueada recebe a mesma resposta `401` de senha errada, e o serviço compara contra o hash fictício, como no login.
- Nova senha rejeitada por política, vazamento ou igualdade **não** conta como falha de bloqueio e não altera nada.

## Nova senha

A mesma política do cadastro e do reset (ver `docs/auth/0004 - Recuperacao de Senha.md`, seção "Política de senha"): 15 a 128 caracteres, sem composição, lista local, palavras do contexto e Pwned Passwords. Além disso, não pode ser igual à atual (conferido com `IPasswordHasher.Verify` contra o hash gravado).

## O que acontece na troca

Numa **única transação** (`IUnitOfWork`, ver `docs/project/0001 - Arquitetura.md`):

1. grava o novo hash e o `password_changed_at`;
2. zera o contador de falhas e o bloqueio;
3. revoga todas as sessões do usuário **exceto a atual**, identificada pelo claim `sid` do token (`RevokeAllActiveByUserExceptSessionAsync`).

- Se qualquer escrita falhar, nada é gravado: a senha antiga continua valendo, as sessões continuam ativas e a resposta é `500`. A mesma chamada funciona numa nova tentativa.
- O hash novo (lento de propósito) e as validações são feitos **antes** da transação, para ela ficar aberta só pelo tempo das escritas.
- O usuário é relido dentro da transação: uma exclusão de conta confirmada nesse meio tempo não é desfeita.
- Token sem `sid` válido (emitido antes da spec de sessões) não tem sessão a preservar: todas são encerradas.
- Quem trocou a senha continua logado: o refresh da sessão atual segue funcionando, o das outras sessões passa a responder `401`.

## Limitações

- Os **access tokens das outras sessões valem até expirar** (no máximo 15 min), porque o JWT é stateless. Só os refresh tokens são revogados.
- Avisar o dono da conta por e-mail sobre a troca é o padrão de mercado e fica como `TODO` para a spec de envio de e-mail.

## Limite de requisições

Política `password-change`: 5 por IP a cada 15 min (ver `docs/auth/0004 - Recuperacao de Senha.md`, seção "Limite de requisições").

## Logs

`Information` com o `externalId` do usuário ao trocar. Rejeições saem em `Warning`. Senhas nunca vão para o log.

## Onde está no código

- Caso de uso: `Auth.Application/UseCases/ChangePassword/`.
- Endpoint: `UserController.ChangePassword`, com o corpo em `Auth.Api/Models/ChangePasswordBody.cs`.
- Revogação das outras sessões: `DapperRefreshTokenRepository.RevokeAllActiveByUserExceptSessionAsync`.
- Claim `sid`: `JwtTokenGenerator.SessionIdClaimType`.
- Limite: `RateLimitingConfiguration.PasswordChangePolicy`.
