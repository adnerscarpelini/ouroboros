# Recuperação de Senha

> **O e-mail ainda não é enviado.** Não existe mensageria no projeto. Por enquanto, o token de recuperação só aparece no **log** do `auth-service`. Diferente do cadastro, ele **nunca** volta na resposta HTTP. Isso é temporário e está marcado com `TODO` no código.

## Visão geral

1. **Solicitação:** o usuário informa login ou e-mail. Um token é gerado e enviado (hoje: logado).
2. **Redefinição:** o usuário envia o token e a nova senha. A senha é trocada e todas as sessões são encerradas.
3. O usuário faz login de novo com a senha nova. **Não existe login automático** após a redefinição.

A senha atual continua valendo até a etapa 2 ser concluída.

## 1. Solicitação

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

| Situação | Status | Corpo |
|---|---|---|
| Usuário apto (token gerado) | `202` | mensagem genérica |
| Login/e-mail inexistente | `202` | mensagem genérica |
| Usuário inativo ou sem e-mail confirmado | `202` | mensagem genérica |
| Campo vazio | `400` | `Login or email is required` |
| Limite de requisições excedido | `429` | vazio |

## 2. Redefinição

1. O cliente envia o token e a nova senha para `POST /api/users/password-reset/confirm`.
2. O token é buscado pelo hash, **só entre tokens do tipo `PasswordReset`**, e precisa estar pendente (não expirado e não usado).
3. O usuário dono do token precisa estar ativo.
4. A nova senha passa pela política de senha (ver abaixo) e não pode ser igual à atual.
5. O token é marcado como usado, a senha é trocada (`password_hash` e `password_changed_at`) e **todos os refresh tokens ativos do usuário são revogados**.

```
POST /api/users/password-reset/confirm
{ "token": "nvZ5Svm_Q-B35qKg...", "newPassword": "Nova@1234" }

204 No Content
```

Erros (todos `400 {"error": "..."}`, exceto o `429`):

| Situação | Mensagem |
|---|---|
| Token vazio, inexistente, de outro tipo, expirado ou já usado | `Invalid or expired password reset token` |
| Usuário inativo | `User is not active` |
| Senha fraca | mensagem da regra violada (ex.: `Password must be at least 8 characters long`) |
| Senha igual à atual | `New password must be different from the current password` |
| Limite de requisições excedido | `429`, corpo vazio |

- **Erro de senha não consome o token.** O usuário pode corrigir a senha e tentar de novo com o mesmo link, enquanto ele não expirar.
- **Requisições simultâneas com o mesmo token:** só uma troca a senha, as outras recebem o erro genérico. O uso é gravado com `UPDATE ... WHERE used_at IS NULL`, então só uma requisição consegue marcar o token (mesmo mecanismo do refresh token em `docs/auth/0003 - Login e Tokens.md`).
- Marcar o token, trocar a senha e revogar as sessões são escritas separadas, sem transação. Se uma escrita depois da marcação falhar, o token fica consumido e o usuário precisa pedir um link novo. Nenhum token fica reutilizável.

## Política de senha

A mesma regra vale para o cadastro e para a redefinição (`PasswordPolicy`, em `Auth.Domain/Policies/`):

- mínimo de 8 caracteres;
- ao menos uma letra maiúscula, uma minúscula e um dígito;
- ao menos um caractere especial (não letra e não dígito).

Ela fica no domínio, e não na entidade `User`, porque a `User` só recebe o hash. A senha em texto puro precisa ser validada antes de chegar na entidade.

## Decisões de segurança

Baseadas no OWASP Forgot Password Cheat Sheet.

- **Token nunca na resposta.** No cadastro quem recebe o token é o próprio dono da conta. Aqui qualquer pessoa pode informar o login de outra, então o token só pode chegar pelo e-mail da conta.
- **Sem enumeração de usuários.** Na solicitação, existir ou não a conta dá a mesma resposta. Na redefinição, todo problema com o token dá a mesma mensagem.
- **Conta inativa é ignorada.** O reset não pode virar atalho pra ativar conta sem confirmar o e-mail.
- **Só o link mais recente vale.** Uma nova solicitação invalida os tokens de recuperação pendentes anteriores.
- **Token forte, uso único, expiração curta.** 32 bytes aleatórios, gravados só como hash SHA-256, válidos por 1h (a confirmação de cadastro vale 24h). Um token de confirmação de e-mail não serve para redefinir senha.
- **Senha nova diferente da atual**, conferida com `IPasswordHasher.Verify` contra o hash gravado.
- **Sessões encerradas.** A troca revoga todos os refresh tokens ativos. Se alguém tinha acesso à conta com a senha antiga, perde a sessão. Os access tokens já emitidos continuam válidos até expirar (no máximo 15 min, ver `docs/auth/0003 - Login e Tokens.md`).
- **Sem login automático** após a redefinição.
- **Limite de requisições** nos dois endpoints (ver abaixo).

Limitações conhecidas:

- Na solicitação, quando a conta existe, a requisição grava no banco e demora um pouco mais. Pela diferença de tempo ainda dá pra inferir se um login existe. Com o envio de e-mail via mensageria (assíncrono), essa diferença deve cair.
- Não existe notificação "sua senha foi alterada". Ela depende do serviço de e-mail.

## Limite de requisições (rate limiting)

Usa o middleware nativo do ASP.NET Core (`AddRateLimiter` / `UseRateLimiter`), sem pacote extra. Janela fixa, por IP, sem fila.

| Política | Endpoint | Limite |
|---|---|---|
| `password-reset-request` | `POST /api/users/password-reset/request` | 5 a cada 15 min |
| `password-reset-confirm` | `POST /api/users/password-reset/confirm` | 10 a cada 15 min |

- A redefinição é mais folgada porque o usuário pode errar a política de senha algumas vezes com o mesmo link.
- Cada política tem seu próprio contador: chamadas de solicitação não consomem o limite da redefinição.
- Acima do limite: `429 Too Many Requests`, com `Warning` no Seq (`Rate limit exceeded for {Path} from {RemoteIp}`).
- Os valores são constantes no código, não configuração.

Para aplicar a outro endpoint, crie uma política em `RateLimitingConfiguration` (com `AddFixedWindowPerIpPolicy`) e marque a action com `[EnableRateLimiting(...)]`.

O limite é por IP de origem. Atrás de um proxy reverso, todos os clientes aparecem com o IP do proxy. Nesse caso, é preciso configurar `ForwardedHeaders` antes de confiar nesse limite.

## Token no banco

Usa a tabela genérica `auth.tokens` (ver `docs/auth/0001 - Confirmacao de Cadastro.md`), com `type = 'PasswordReset'`. Não houve migration.

- **Usado:** `used_at` preenchido na redefinição.
- **Invalidado:** `expires_at` antecipado pro instante de uma nova solicitação (e `updated_at` atualizado). A tabela não tem coluna de revogação, e preencher `used_at` diria que o token foi usado, o que não é verdade. Tokens de outros tipos (ex.: `EmailConfirmation`) não são afetados.

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
- Senha redefinida: `Information`, com o id do usuário.
- Solicitação ou redefinição rejeitada: `Warning`, com o motivo.
- Limite excedido: `Warning`, com o caminho e o IP.

O log do token é sensível: qualquer pessoa com acesso ao Seq consegue redefinir a senha de qualquer conta. Não pode ir pra um ambiente real assim.

## Onde está no código

- Casos de uso: `Auth.Application/UseCases/RequestPasswordReset/` e `Auth.Application/UseCases/ResetPassword/`.
- Endpoints: `UserController.RequestPasswordReset` e `UserController.ResetPassword` em `Auth.Api/Controllers/`.
- Política de senha: `Auth.Domain/Policies/PasswordPolicy.cs` (usada também pelo `RegisterUserInteractor`).
- Troca de senha: `User.ChangePassword`. Token pendente: `Token.IsPending`. Ambos em `Auth.Domain/Entities/`.
- Rate limiting: `Auth.Api/Configuration/RateLimitingConfiguration.cs`, registrado em `Program.cs`.
- Busca por login ou e-mail: `DapperUserRepository.GetByLoginOrEmailAsync`.
- Invalidação dos tokens pendentes: `DapperTokenRepository.InvalidatePendingByUserAsync`.
- Uso concorrente do token: `DapperTokenRepository.TryMarkAsUsedAsync`.
- Revogação das sessões: `DapperRefreshTokenRepository.RevokeAllActiveByUserAsync`.

## Quando existir envio de e-mail

- Remover `PasswordResetToken` (e `UserId`) de `RequestPasswordResetResponse`.
- Remover o log do token em `UserController.RequestPasswordReset`.
- Enviar o token num link pra tela de redefinição, que chama `POST /api/users/password-reset/confirm`.
- Enviar a notificação "sua senha foi alterada" após a redefinição.
