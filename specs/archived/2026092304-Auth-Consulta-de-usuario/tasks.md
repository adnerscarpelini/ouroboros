# 2026092304 - Consulta de usuario — Tarefas

- [x] **Dev** — Configurar autenticação JWT Bearer na API (`AddAuthentication().AddJwtBearer` com `JwtSettings`, validação de issuer/audience/lifetime/assinatura, `UseAuthentication`/`UseAuthorization`)
- [x] **Dev** — Criar caso de uso `GetUser` em `Auth.Application`, recebendo o solicitante (externalId + perfil, vindos do token) e o critério de busca; aplica a regra Admin/ownership antes de consultar o repositório
- [x] **Dev** — Criar `GetUserResponse` com somente os campos permitidos (`ExternalId`, `Login`, `FullName`, `Email`, `Role`, `Active`, `CreatedAt`)
- [x] **Dev** — Criar endpoint `GET /api/users/{externalId}` (`[Authorize]`)
- [x] **Dev** — Criar endpoint `POST /api/users/search` (`[Authorize]`) com body contendo **exatamente um** de `login` ou `email` (nenhum ou ambos → 400); usuário inexistente → 404; sem permissão → 403
- [x] **Dev** — Atualizar a collection Postman (novos requests com Bearer token)
- [x] **DBA** — Adicionar `GetByEmailAsync` ao `IUserRepository`/`DapperUserRepository` (índices únicos de `login` e `email` já cobrem as buscas; sem migration)
- [x] **Tester** — Cobrir: busca por externalId, login e email; 404; filtro inválido (nenhum/ambos); `User` consultando a si mesmo; `User` consultando outro → negado sem consultar o repositório; `Admin` consultando qualquer um; resposta sem campos críticos
- [x] **Tech Writer** — Documentar em `docs/auth/` os endpoints de consulta, a autenticação Bearer exigida, a regra de acesso e os campos retornados
