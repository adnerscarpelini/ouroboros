# Confirmação de Cadastro

> **O e-mail ainda não é enviado.** Não existe mensageria no projeto. Por enquanto, o token de confirmação só aparece no **log** do `auth-service` (Seq). Ele **nunca** volta na resposta HTTP. Isso é temporário e está marcado com `TODO` no código.

## Fluxo

1. `POST /api/users` recebe login, nome, e-mail e senha. O body não aceita perfil: todo cadastro nasce com perfil `User` (ver `docs/auth/0005 - Perfis de Acesso.md`).
2. Se o login estiver ocupado, devolve `400`. Se o e-mail estiver ocupado, nada é criado, mas a resposta é a mesma do sucesso (ver "Login e e-mail já usados").
3. Se estiver tudo livre, o usuário é criado **inativo** (`active = false`, `email_confirmed = false`) e é gerado um token de confirmação válido por **24h**.
4. O token é enviado pro e-mail informado (hoje: logado).
5. O dono do e-mail envia o token para `POST /api/users/confirm-email`.
6. Se o token for válido, o usuário passa a `active = true` e `email_confirmed = true`, e o token é marcado como usado.

## Endpoints

Cadastro:

```
POST /api/users
{ "login": "jdoe", "fullName": "John Doe", "email": "jdoe@example.com", "password": "S3cret!1" }

202 Accepted
{ "message": "If the email is available, a confirmation link will be sent to it." }
```

| Situação | Status | Corpo |
|---|---|---|
| Usuário criado | `202` | mensagem genérica |
| E-mail já usado | `202` | mensagem genérica |
| Login já usado | `400` | `Login already in use` |
| Dados inválidos ou senha fora da política | `400` | mensagem da regra violada |
| Limite de requisições excedido | `429` | vazio |

A resposta não traz `id` nem header `Location`. O `externalId` do usuário é obtido depois do login, pela consulta (ver `docs/auth/0006 - Consulta de Usuario.md`).

Confirmação:

```
POST /api/users/confirm-email
{ "token": "UO7xZ5mw..." }

200 OK
{ "userId": "...", "login": "jdoe", "email": "jdoe@example.com" }
```

## Erros da confirmação

Todos devolvem `400 {"error": "..."}`, com o motivo logado em `Warning` no Seq.

| Situação | Mensagem |
|---|---|
| Token vazio | `Confirmation token is required` |
| Token não existe | `Invalid confirmation token` |
| Token já usado | `Token has already been used` |
| Token expirado | `Token has expired` |

## Login e e-mail já usados

A checagem ignora contas abandonadas (ver abaixo).

- **Login usado:** `400 Login already in use`. Quem escolhe o login precisa saber que ele está ocupado.
- **E-mail usado:** mesma resposta `202` do sucesso. Nenhum usuário ou token é criado e fica um `Warning` no Seq (`User registration ignored: email already in use`), sem o e-mail. Quando existir envio de e-mail, o dono deve ser avisado da tentativa (`TODO` no `RegisterUserInteractor`).

## Cadastro abandonado

Uma conta é **abandonada** quando o e-mail nunca foi confirmado e ela não tem token de confirmação pendente (o token expirou, ou seja, passaram as 24h).

- Um cadastro novo com o mesmo login ou o mesmo e-mail de uma conta abandonada **remove** essa conta e segue normalmente.
- Se o login bater com uma conta abandonada e o e-mail com outra, as duas são removidas.
- Dentro das 24h, a conta pendente continua reservando o login e o e-mail.
- A remoção leva junto os tokens e refresh tokens da conta (`ON DELETE CASCADE`).

Não existe reenvio de token. Se o token expirar, a pessoa faz o cadastro de novo.

## Decisões de segurança

Baseadas no OWASP Authentication Cheat Sheet.

- **Token nunca na resposta.** Quem cadastra só declara ser dono do e-mail. Se o token voltasse na resposta, qualquer pessoa poderia cadastrar e confirmar um e-mail que não é dela. O token só pode chegar pelo próprio e-mail.
- **Sem enumeração de e-mail.** E-mail novo ou já cadastrado dá a mesma resposta.
- **E-mail não fica sequestrado.** Um cadastro de e-mail alheio nunca confirmado deixa de bloquear o dono depois de 24h.
- **Token forte, uso único, expiração.** Gravado só como hash SHA-256, válido por 24h.
- **Limite de requisições:** política `user-register`, 5 cadastros por IP a cada 15 min (ver `docs/auth/0004 - Recuperacao de Senha.md`, seção "Limite de requisições").

Limitações conhecidas:

- **Login enumerável.** O `400 Login already in use` revela que um login existe. É aceito: o dado protegido é o e-mail.
- **Diferença de tempo.** Com e-mail novo a requisição grava no banco e demora um pouco mais. Pela diferença de tempo ainda dá pra inferir se um e-mail existe. Com o envio de e-mail via mensageria (assíncrono), essa diferença deve cair.
- **Pre-account takeover.** Um atacante pode cadastrar o e-mail da vítima com uma senha que só ele sabe. Se a vítima clicar no link de confirmação, a conta fica ativa com a senha do atacante. A mitigação de mercado é "confirma o e-mail primeiro, define a senha depois", que ainda não foi feita.
- **Cadastros simultâneos** com o mesmo login ou e-mail: o índice único do banco barra o segundo, que recebe `500`.

## Tabela `auth.tokens`

Tabela genérica, pensada para reaproveitar em outros fluxos de token sem criar uma tabela nova por caso.

- `user_id`: FK para `auth.users`, com `ON DELETE CASCADE`.
- `type`: nome do enum `TokenType` gravado como texto (hoje `EmailConfirmation` e `PasswordReset`, ver `docs/auth/0004 - Recuperacao de Senha.md`). Para um fluxo novo, adicione um valor ao enum.
- `token_hash`: hash SHA-256 do token, com índice único. **O token em texto puro nunca é gravado.** A busca é feita pelo hash.
- `expires_at` / `used_at`: validade e uso único.

## Logs

- Token gerado: `Information`, com o id do usuário e o **token em texto puro** (temporário, só pra dev).
- E-mail já usado: `Warning`, sem o e-mail.
- Cadastro rejeitado (`400`): `Warning`, com o motivo.

O log do token é sensível: qualquer pessoa com acesso ao Seq consegue confirmar qualquer cadastro. Não pode ir pra um ambiente real assim.

## Onde está no código

- Domínio: `Token`, `TokenType` e `User.ConfirmEmail()` em `Auth.Domain/Entities/`.
- Casos de uso: `RegisterUser` (checagem de login/e-mail, cadastro abandonado e geração do token) e `ConfirmEmail` em `Auth.Application/UseCases/`.
- Endpoint: `UserController.Register` em `Auth.Api/Controllers/`.
- Geração e hash do token: `Sha256TokenGenerator` em `Auth.Infrastructure/Security/`.
- Token pendente: `DapperTokenRepository.ExistsPendingByUserAsync`. Remoção da conta: `DapperUserRepository.RemoveAsync`.
- Rate limiting: `Auth.Api/Configuration/RateLimitingConfiguration.cs`.
- Migrations: `V20260923100000__CreateTokensTable.sql` e `V20260923200000__CascadeUserTokensOnDelete.sql`.

## Quando existir envio de e-mail

- Remover `EmailConfirmationToken` (e `UserId`) de `RegisterUserResponse`.
- Remover o log do token em `UserController.Register`.
- Enviar o token num link que chama `POST /api/users/confirm-email`.
- Avisar o dono do e-mail quando alguém tentar cadastrar um e-mail já usado.
