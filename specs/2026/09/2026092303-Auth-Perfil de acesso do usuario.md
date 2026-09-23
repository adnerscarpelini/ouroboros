# 2026092303 - Perfil de acesso do usuario

**Data:** 23/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

Hoje os users apenas são cadastrados, sem nenhum tipo de perfil. O usuário pediu uma coluna com o tipo de perfil do user, inicialmente algo como Administrator e User, com o nome decidido conforme o padrão de mercado, e que as estruturas atuais (banco, APIs etc.) sejam ajustadas para contemplar isso. Pediu que tudo siga o padrão seguro de mercado adotado por empresas grandes.

## Análise

Extensão do `auth-service`: perfil de acesso é parte do domínio de identidade, não justifica serviço novo. Nome adotado: **`Role`** (coluna `role`), valores **`User`** e **`Admin`** — padrão do ASP.NET Core (`ClaimTypes.Role`, `[Authorize(Roles = ...)]`) e dos principais IdPs (Keycloak, Auth0, Entra ID); "Profile" fica reservado para dados pessoais.

Decisões de segurança:
- Princípio do menor privilégio: todo cadastro nasce `User`; o request de cadastro **não** aceita perfil (impede auto-promoção por mass assignment).
- O primeiro `Admin` é promovido manualmente via SQL, com o procedimento documentado. Endpoint de troca de perfil fica para spec futura.
- O perfil vai no JWT como claim de role, para que a autorização não precise consultar o banco a cada request. Como consequência, mudança de perfil só reflete após a renovação do access token — mitigado pela vida curta do JWT (spec [2026092202](2026092202-Auth-Parametros%20de%20configuracao%20de%20autenticacao.md)).
- No banco, `role` é `text` com `CHECK` restringindo aos valores válidos; usuários existentes recebem `User` via `DEFAULT` na migration.

Pré-requisito da spec [2026092304](2026092304-Auth-Consulta%20de%20usuario.md), que usa o claim de perfil para autorizar a consulta.

## Tarefas

- [x] **Dev** — Criar enum `UserRole { User, Admin }` em `Auth.Domain` e propriedade `Role` em `User`; `User.Create` atribui sempre `UserRole.User`; `User.Rehydrate` recebe o perfil
- [x] **Dev** — Garantir que `RegisterUserRequest` não aceita perfil; `RegisterUserResponse` passa a devolver `Role`
- [x] **Dev** — Incluir o claim de role no JWT: alterar `IJwtTokenGenerator.Generate`/`JwtTokenGenerator` e as chamadas em `LoginInteractor` e `RefreshAccessTokenInteractor`
- [x] **Dev** — Atualizar a collection Postman (resposta do cadastro com `role`)
- [x] **DBA** — Migration adicionando `role text NOT NULL DEFAULT 'User'` em `auth.users`, com `CHECK (role IN ('User', 'Admin'))`
- [x] **DBA** — Ajustar `DapperUserRepository` (INSERT, UPDATE, todos os SELECTs e mapeamento `UserRow` ↔ `UserRole`)
- [x] **Tester** — Cobrir: cadastro gera `User`; `Rehydrate` restaura o perfil; login emite JWT com o claim de role; refresh mantém o perfil do usuário
- [ ] **Tech Writer** — Documentar em `docs/auth/` os perfis existentes, o claim no JWT e o procedimento SQL para promover o primeiro `Admin`
