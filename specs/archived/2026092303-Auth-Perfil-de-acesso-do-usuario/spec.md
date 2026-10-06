# 2026092303 - Perfil de acesso do usuario

**Data:** 23/09/2026
**Servico(s):** auth-service

## Solicitação

Hoje os users apenas são cadastrados, sem nenhum tipo de perfil. O usuário pediu uma coluna com o tipo de perfil do user, inicialmente algo como Administrator e User, com o nome decidido conforme o padrão de mercado, e que as estruturas atuais (banco, APIs etc.) sejam ajustadas para contemplar isso. Pediu que tudo siga o padrão seguro de mercado adotado por empresas grandes.

## Análise

Extensão do `auth-service`: perfil de acesso é parte do domínio de identidade, não justifica serviço novo. Nome adotado: **`Role`** (coluna `role`), valores **`User`** e **`Admin`** — padrão do ASP.NET Core (`ClaimTypes.Role`, `[Authorize(Roles = ...)]`) e dos principais IdPs (Keycloak, Auth0, Entra ID); "Profile" fica reservado para dados pessoais.

Decisões de segurança:
- Princípio do menor privilégio: todo cadastro nasce `User`; o request de cadastro **não** aceita perfil (impede auto-promoção por mass assignment).
- O primeiro `Admin` é promovido manualmente via SQL, com o procedimento documentado. Endpoint de troca de perfil fica para spec futura.
- O perfil vai no JWT como claim de role, para que a autorização não precise consultar o banco a cada request. Como consequência, mudança de perfil só reflete após a renovação do access token — mitigado pela vida curta do JWT (spec 2026092202).
- No banco, `role` é `text` com `CHECK` restringindo aos valores válidos; usuários existentes recebem `User` via `DEFAULT` na migration.

Pré-requisito da spec 2026092304, que usa o claim de perfil para autorizar a consulta.
