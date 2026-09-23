# Recuperação de Senha

> **O e-mail ainda não é enviado.** Não existe mensageria no projeto. Por enquanto, o token de recuperação só aparece no **log** do `auth-service`. Diferente do cadastro, ele **nunca** volta na resposta HTTP. Isso é temporário e está marcado com `TODO` no código.

O fluxo tem duas etapas:

1. **Solicitação:** o usuário informa login ou e-mail e recebe um link com token. Etapa descrita neste documento.
2. **Redefinição:** o usuário envia o token e a nova senha. Etapa ainda não implementada (spec `2026092302`).

## Solicitação

1. O cliente envia login **ou** e-mail para `POST /api/users/password-reset/request`.
2. O `auth-service` busca o usuário pelo login ou pelo e-mail. Se o valor bater com o login de uma conta e o e-mail de outra, o login tem prioridade.
3. Se o usuário não existir, estiver inativo ou não tiver confirmado o e-mail, nada acontece.
4. Se estiver apto, os tokens de recuperação pendentes dele são invalidados e um novo token é gerado, válido por **1h**.
5. A resposta é sempre a mesma, em todos os casos acima.

```
POST /api/users/password-reset/request
{ "loginOrEmail": "jdoe" }

202 Accepted
{ "message": "If the account exists, a password reset link will be sent to its email." }
```

A conta não muda nesta etapa. A senha atual continua valendo até a redefinição ser concluída.

## Respostas

| Situação | Status | Corpo |
|---|---|---|
| Usuário apto (token gerado) | `202` | mensagem genérica |
| Login/e-mail inexistente | `202` | mensagem genérica |
| Usuário inativo ou sem e-mail confirmado | `202` | mensagem genérica |
| Campo vazio | `400` | `{"error": "Login or email is required"}` |
| Limite de requisições excedido | `429` | vazio |

## Decisões de segurança

Baseadas no OWASP Forgot Password Cheat Sheet.

- **Token nunca na resposta.** No cadastro quem recebe o token é o próprio dono da conta. Aqui qualquer pessoa pode informar o login de outra, então o token só pode chegar pelo e-mail da conta.
- **Sem enumeração de usuários.** Existir ou não a conta dá a mesma resposta, com o mesmo status.
- **Conta inativa é ignorada.** O reset não pode virar atalho pra ativar conta sem confirmar o e-mail.
- **Só o link mais recente vale.** Uma nova solicitação invalida os tokens de recuperação pendentes anteriores.
- **Token forte, uso único, expiração curta.** 32 bytes aleatórios, gravados só como hash SHA-256, válidos por 1h (a confirmação de cadastro vale 24h).
- **Limite de requisições** no endpoint (ver abaixo).

Limitação conhecida: quando a conta existe, a requisição grava no banco e demora um pouco mais. Pela diferença de tempo ainda dá pra inferir se um login existe. Com o envio de e-mail via mensageria (assíncrono), essa diferença deve cair.

## Limite de requisições (rate limiting)

Usa o middleware nativo do ASP.NET Core (`AddRateLimiter` / `UseRateLimiter`), sem pacote extra.

- Política `password-reset`: **5 requisições a cada 15 minutos por IP**, janela fixa, sem fila.
- Acima do limite: `429 Too Many Requests`, com `Warning` no Seq (`Rate limit exceeded for {Path} from {RemoteIp}`).
- Aplicada no endpoint com `[EnableRateLimiting(RateLimitingConfiguration.PasswordResetPolicy)]`.
- Os valores são constantes no código, não configuração.

Para aplicar a outro endpoint, reuse a política ou crie uma nova em `RateLimitingConfiguration` e marque a action com `[EnableRateLimiting(...)]`.

O limite é por IP de origem. Atrás de um proxy reverso, todos os clientes aparecem com o IP do proxy. Nesse caso, é preciso configurar `ForwardedHeaders` antes de confiar nesse limite.

## Token no banco

Usa a tabela genérica `auth.tokens` (ver `docs/auth/0001 - Confirmacao de Cadastro.md`), com `type = 'PasswordReset'`. Não houve migration.

**Invalidar** um token pendente significa antecipar o `expires_at` pro instante da nova solicitação (e atualizar `updated_at`). A tabela não tem coluna de revogação, e preencher `used_at` diria que o token foi usado, o que não é verdade. Tokens de outros tipos (ex.: `EmailConfirmation`) não são afetados.

Pra ver os tokens de um usuário no Postgres local:

```sql
SELECT t.type, t.created_at, t.expires_at, t.used_at
FROM auth.tokens AS t
JOIN auth.users AS u ON u.id = t.user_id
WHERE u.login = 'jdoe'
ORDER BY t.id;
```

## Logs

- Token gerado: `Information`, com o id do usuário e o **token em texto puro** (temporário, só pra dev).
- Campo vazio: `Warning`, com o motivo.
- Limite excedido: `Warning`, com o caminho e o IP.

O log do token é sensível: qualquer pessoa com acesso ao Seq consegue redefinir a senha de qualquer conta. Não pode ir pra um ambiente real assim.

## Onde está no código

- Caso de uso: `Auth.Application/UseCases/RequestPasswordReset/`.
- Endpoint: `UserController.RequestPasswordReset` em `Auth.Api/Controllers/`.
- Rate limiting: `Auth.Api/Configuration/RateLimitingConfiguration.cs`, registrado em `Program.cs`.
- Busca por login ou e-mail: `DapperUserRepository.GetByLoginOrEmailAsync`.
- Invalidação dos tokens pendentes: `DapperTokenRepository.InvalidatePendingByUserAsync`.
- Tipo do token: `TokenType.PasswordReset` em `Auth.Domain/Entities/`.

## Quando existir envio de e-mail

- Remover `PasswordResetToken` (e `UserId`) de `RequestPasswordResetResponse`.
- Remover o log do token em `UserController.RequestPasswordReset`.
- Enviar o token num link pra tela de redefinição de senha.
