# Consulta de Usuário

Busca os dados não sensíveis de um usuário por `externalId`, login ou e-mail.

## Autenticação

Os dois endpoints exigem access token:

```
Authorization: Bearer <accessToken>
```

Sem token, ou com token inválido/expirado → `401`. Como o token é validado está em `docs/auth/0003 - Login e Tokens.md`.

## Regra de acesso

| Perfil do solicitante | Pode consultar |
|---|---|
| `Admin` | Qualquer usuário |
| `User` | Só a si mesmo |

- O perfil e a identidade vêm do token (`sub`, `unique_name`, `email`, `role`), nunca do body.
- `User` pedindo outro usuário → `403`. A negação acontece **antes** de consultar o banco, então a resposta é a mesma exista ou não a conta pedida. Isso impede enumeração de usuários.
- Qualquer valor de `role` diferente de exatamente `Admin` é tratado como sem privilégio.
- A regra fica no caso de uso (`GetUserInteractor`), não só no controller, e é coberta por teste.

## Endpoints

Por `externalId` (identificador opaco, pode ir na URL):

```
GET /api/users/3b8f55c5-155c-41a7-9949-e5b7d0d83ffd
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

Por login ou e-mail, no body:

```
POST /api/users/search
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
{ "login": "jdoe" }
```

```
POST /api/users/search
{ "email": "jdoe@example.com" }
```

- O body precisa ter **exatamente um** dos dois campos.
- Login e e-mail vão no body, não na query string, porque a URL fica em logs de proxy, gateway e histórico do navegador (OWASP).
- A busca é exata. Espaços nas pontas são removidos.
- Conta excluída não aparece na busca: devolve `404`, como se não existisse (ver `docs/auth/0007 - Exclusao de Conta.md`).

Resposta dos dois:

```
200 OK
{
  "externalId": "3b8f55c5-155c-41a7-9949-e5b7d0d83ffd",
  "login": "jdoe",
  "fullName": "John Doe",
  "email": "jdoe@example.com",
  "role": "User",
  "active": true,
  "createdAt": "2026-09-23T20:06:26.498457+00:00"
}
```

## Campos retornados

A resposta (`GetUserResponse`) é uma allowlist: só os campos acima. **Nunca** saem:

- `id` interno;
- `password_hash`, `password_changed_at`;
- `last_login_at`, `email_confirmed`;
- tokens ou hashes de token.

Um teste (`ShouldExposeOnlyAllowedFieldsInResponse`) falha se alguém adicionar um campo ao record sem atualizar a allowlist.

## Erros

Todos com corpo `{"error": "..."}`, exceto o `401`.

| Situação | Status | Mensagem |
|---|---|---|
| Sem token, token inválido ou expirado | `401` | *(sem corpo)* |
| Nenhum ou os dois campos no body do `search` | `400` | `Exactly one search criterion (externalId, login or email) is required` |
| `User` consultando outro usuário | `403` | `Access denied` |
| Usuário não existe ou foi excluído | `404` | `User not found` |

`400`, `403` e `404` são logados em `Warning` no Seq, com o `externalId` do solicitante.

## Onde está no código

- Caso de uso: `Auth.Application/UseCases/GetUser/`.
- Endpoints: `GetById` e `Search` em `Auth.Api/Controllers/UserController.cs`. Body do search: `Auth.Api/Models/SearchUserBody.cs`.
- Exceções: `AccessDeniedException` e `UserNotFoundException` em `Auth.Domain/Exceptions/`.
- Busca por e-mail: `DapperUserRepository.GetByEmailAsync`. Os índices únicos de `login` e `email` cobrem as buscas (o de `email` é parcial, só para contas não excluídas).
- Perfis: `docs/auth/0005 - Perfis de Acesso.md`.
