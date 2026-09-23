# 2026092304 - Consulta de usuario

**Data:** 23/09/2026
**Status:** Em andamento
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

Depende da spec [2026092303](2026092303-Auth-Perfil%20de%20acesso%20do%20usuario.md) (claim de role no JWT).

## Tarefas

- [x] **Dev** — Configurar autenticação JWT Bearer na API (`AddAuthentication().AddJwtBearer` com `JwtSettings`, validação de issuer/audience/lifetime/assinatura, `UseAuthentication`/`UseAuthorization`)
- [x] **Dev** — Criar caso de uso `GetUser` em `Auth.Application`, recebendo o solicitante (externalId + perfil, vindos do token) e o critério de busca; aplica a regra Admin/ownership antes de consultar o repositório
- [x] **Dev** — Criar `GetUserResponse` com somente os campos permitidos (`ExternalId`, `Login`, `FullName`, `Email`, `Role`, `Active`, `CreatedAt`)
- [x] **Dev** — Criar endpoint `GET /api/users/{externalId}` (`[Authorize]`)
- [x] **Dev** — Criar endpoint `POST /api/users/search` (`[Authorize]`) com body contendo **exatamente um** de `login` ou `email` (nenhum ou ambos → 400); usuário inexistente → 404; sem permissão → 403
- [x] **Dev** — Atualizar a collection Postman (novos requests com Bearer token)
- [x] **DBA** — Adicionar `GetByEmailAsync` ao `IUserRepository`/`DapperUserRepository` (índices únicos de `login` e `email` já cobrem as buscas; sem migration)
- [x] **Tester** — Cobrir: busca por externalId, login e email; 404; filtro inválido (nenhum/ambos); `User` consultando a si mesmo; `User` consultando outro → negado sem consultar o repositório; `Admin` consultando qualquer um; resposta sem campos críticos
- [x] **Tech Writer** — Documentar em `docs/auth/` os endpoints de consulta, a autenticação Bearer exigida, a regra de acesso e os campos retornados
