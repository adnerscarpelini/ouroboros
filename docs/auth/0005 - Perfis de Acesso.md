# Perfis de Acesso

Todo usuário tem um perfil (`role`) que define o que ele pode fazer nas rotas protegidas.

| Perfil | Quem | Pode |
|---|---|---|
| `User` | Todo usuário cadastrado | Acessar só os próprios dados |
| `Admin` | Promovido manualmente | Acessar dados de qualquer usuário |

O nome segue o padrão de mercado: `role` no ASP.NET Core (`[Authorize(Roles = ...)]`), Keycloak, Auth0 e Entra ID. "Profile" fica reservado para dados pessoais.

## Regras

- **Menor privilégio:** todo cadastro nasce `User`. O `POST /api/users` não aceita perfil no body, então ninguém se promove sozinho.
- **Promoção só por SQL**, feita por quem tem acesso ao banco (ver [Promover um Admin](#promover-um-admin)). Ainda não existe endpoint de troca de perfil.
- O perfil vai no access token como claim `role`. A autorização lê o token, sem consultar o banco a cada request.

## Mudança de perfil e o token

Como o perfil está no JWT, uma mudança só vale no **próximo token emitido**:

- **Promoção:** vale no próximo login ou refresh. Os dois leem o perfil atual do banco.
- **Rebaixamento:** o access token já emitido continua com o perfil antigo até expirar (`Jwt:AccessTokenExpirationMinutes`, padrão 15 min). Para impedir que a sessão seja renovada, revogue também os refresh tokens do usuário (abaixo).

A vida curta do access token é o que limita essa janela (ver `docs/auth/0002 - Configuracao JWT.md`).

## Promover um Admin

1. O usuário precisa já estar cadastrado e com o e-mail confirmado.
2. Conecte no banco do `auth-service` com a role dona dele. Localmente, via Docker:
   ```
   docker exec -it ouroboros-postgres psql -U auth_service -d ouroboros_auth
   ```
3. Promova pelo login:
   ```sql
   UPDATE auth.users
   SET
       role = 'Admin',
       updated_at = now()
   WHERE login = 'jdoe';
   ```
   Confira que a saída foi `UPDATE 1`.
4. O usuário faz login de novo (ou refresh) para receber um token com `role = Admin`.

Para rebaixar, use o mesmo `UPDATE` com `role = 'User'` e revogue as sessões ativas:

```sql
UPDATE auth.refresh_tokens
SET
    revoked_at = now(),
    updated_at = now()
WHERE
    user_id = (SELECT users.id FROM auth.users AS users WHERE users.login = 'jdoe')
    AND revoked_at IS NULL;
```

Em ambiente compartilhado ou de produção, a promoção é uma mudança de acesso. Faça com registro de quem pediu e quem aprovou.

## Banco

- Coluna `role text NOT NULL DEFAULT 'User'` em `auth.users`.
- `CHECK (role IN ('User', 'Admin'))`: o banco rejeita qualquer outro valor, inclusive vindo de SQL manual.
- Grava o nome do enum `UserRole` como texto. Para um perfil novo, adicione o valor ao enum **e** uma migration nova ajustando o `CHECK`.
- Usuários que já existiam antes da migration receberam `User` pelo `DEFAULT`.

## Onde está no código

- Enum e regra de criação: `UserRole` e `User.Create` em `Auth.Domain/Entities/`.
- Claim no token: `JwtTokenGenerator` em `Auth.Infrastructure/Security/`.
- Mapeamento texto ↔ enum: `DapperUserRepository`.
- Migration: `V20260923180000__AddRoleToUsers.sql`.
- Uso do perfil para autorizar: `docs/auth/0006 - Consulta de Usuario.md`.
