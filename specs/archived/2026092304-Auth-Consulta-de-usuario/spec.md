# 2026092304 - Consulta de usuario

**Data:** 23/09/2026
**Servico(s):** auth-service

## Solicitação

O usuário pediu um método de consulta (get) do user, por externalId, Login ou Email, que traga os dados dele — nome, email, ativo, data de criação e externalId — nunca trazendo informações críticas. Pediu que tudo siga o padrão seguro de mercado adotado por empresas grandes.

## Análise

Extensão do `auth-service`. Ponto crítico encontrado: a API hoje **não valida JWT em nenhum endpoint** (`Program.cs` sem `AddAuthentication`/`UseAuthorization`). Expor a consulta assim permitiria enumeração de usuários e vazamento de dados pessoais, então esta spec também liga a autenticação JWT na API.

Decisões de segurança:
- **Autenticação obrigatória** (JWT Bearer validando issuer, audience, lifetime e assinatura com o `JwtSettings` existente). Sem token → 401.
- **Autorização por perfil (RBAC) + ownership**, aplicada no caso de uso (testável, não só no controller): `Admin` consulta qualquer usuário; `User` consulta apenas a si mesmo. Pedir outro usuário → 403, decidido **antes** de consultar o banco, para não revelar se a conta existe.
- **Sem dado pessoal na URL** (OWASP): busca por login/email via `POST /api/users/search` com o filtro no body — query string acaba em logs de proxy, gateway e histórico. Busca por externalId (identificador opaco) segue `GET /api/users/{externalId}`.
- **DTO de saída próprio, com allowlist de campos**: `externalId`, `login`, `fullName`, `email`, `role`, `active`, `createdAt`. Nunca expor `id` interno, `password_hash`, `password_changed_at`, `last_login_at`, `email_confirmed`, tokens ou hashes.

Depende da spec 2026092303 (claim de role no JWT).
