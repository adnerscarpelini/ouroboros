# Perfis de Acesso

Todo usuário tem um perfil (`role`) que define o que ele pode fazer nas rotas protegidas.

| Perfil | Quem | Pode |
|---|---|---|
| `User` | Todo usuário cadastrado | Acessar só os próprios dados e excluir a própria conta |
| `Admin` | Promovido manualmente | Acessar dados de qualquer usuário e excluir qualquer conta (ver `docs/auth/0007 - Exclusao de Conta.md`) |

O nome segue o padrão de mercado: `role` no ASP.NET Core (`[Authorize(Roles = ...)]`), Keycloak, Auth0 e Entra ID. "Profile" fica reservado para dados pessoais.

## Regras

- **Menor privilégio:** todo cadastro nasce `User`. O `POST /api/users` não aceita perfil no body, então ninguém se promove sozinho.
- **Promoção só por SQL**, feita por quem tem acesso ao banco (ver [Promover um Admin](#promover-um-admin)). Ainda não existe endpoint de troca de perfil.
- O perfil vai no access token como claim `role`, para outros serviços e para decisões não sensíveis.
- **Ações privilegiadas leem o perfil do banco, não o claim.** Consultar uma conta alheia e excluir uma conta alheia decidem "é Admin?" pelo `role` gravado em `auth.users` no momento da requisição. Isso vale para toda ação privilegiada nova.

## Mudança de perfil e o token

O claim `role` do token só muda no **próximo token emitido**, mas isso não afeta as ações privilegiadas do `auth-service`:

- **No `auth-service`, vale na hora.** Promoção e rebaixamento valem na próxima requisição de consulta ou exclusão, mesmo com o access token antigo ainda dentro do prazo, porque o perfil é lido do banco.
- **O claim `role` do token:** a promoção aparece no próximo login ou refresh (os dois leem o perfil atual do banco). Num rebaixamento, o token já emitido continua com o claim antigo até expirar (`Jwt:AccessTokenExpirationMinutes`, padrão 15 min).
- **Limitação em outros serviços.** Um serviço que validar o JWT sozinho e decidir pelo claim `role` continua enxergando o perfil antigo até o `exp` do token. A janela de 15 min é o padrão aceito para access tokens curtos (ver `docs/auth/0002 - Configuracao JWT.md`).
- **Conta excluída** perde o acesso na hora nos endpoints do `auth-service` (`401 Invalid access token`), mesmo com o token ainda válido.

Para impedir que a sessão rebaixada seja renovada com o perfil antigo no claim, revogue também os refresh tokens do usuário (abaixo).

## Promover um Admin

1. O usuário precisa já estar cadastrado e com o e-mail confirmado.
2. Conecte no banco do `auth-service` com o login dono dele (`auth_service`). Localmente, via Docker (a senha é a `AUTH_DB_PASSWORD` do `.env`):
   ```
   docker exec -it ouroboros-sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U auth_service -d ouroboros_auth
   ```
   No `sqlcmd`, cada comando só executa depois de uma linha com `GO`. Alternativamente, use o SSMS ou o Azure Data Studio em `localhost,1433`.
3. Promova pelo login:
   ```sql
   UPDATE auth.users
   SET
       role = 'Admin',
       updated_at = SYSDATETIMEOFFSET()
   WHERE login = 'jdoe';
   GO
   ```
   Confira que a saída foi `(1 rows affected)`.
4. O usuário faz login de novo (ou refresh) para receber um token com `role = Admin`.

Para rebaixar, use o mesmo `UPDATE` com `role = 'User'` e revogue as sessões ativas:

```sql
UPDATE auth.refresh_tokens
SET
    revoked_at = SYSDATETIMEOFFSET(),
    updated_at = SYSDATETIMEOFFSET()
WHERE
    user_id = (SELECT users.id FROM auth.users AS users WHERE users.login = 'jdoe')
    AND revoked_at IS NULL;
GO
```

Em ambiente compartilhado ou de produção, a promoção é uma mudança de acesso. Faça com registro de quem pediu e quem aprovou.

## Banco

- Coluna `role nvarchar(20) NOT NULL DEFAULT 'User'` em `auth.users`.
- `CHECK (role IN ('User', 'Admin'))`: o banco rejeita qualquer outro valor, inclusive vindo de SQL manual.
- Grava o nome do enum `UserRole` como texto. Para um perfil novo, adicione o valor ao enum **e** uma migration nova ajustando o `CHECK`.
- Usuários que já existiam antes da migration receberam `User` pelo `DEFAULT`.

## Onde está no código

- Enum e regra de criação: `UserRole` e `User.Create` em `Auth.Domain/Entities/`.
- Claim no token: `JwtTokenGenerator` em `Auth.Infrastructure/Security/`.
- Mapeamento texto ↔ enum: `DapperUserRepository`.
- Migration: `V20260923180000__AddRoleToUsers.sql`.
- Uso do perfil para autorizar: `docs/auth/0006 - Consulta de Usuario.md`.
