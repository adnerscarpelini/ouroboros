# Política de Identidade

Como o `auth-service` compara e valida **login** e **e-mail**. Baseada no `NormalizedUserName`/`NormalizedEmail` do ASP.NET Core Identity e no OWASP Input Validation Cheat Sheet (allowlist).

## Normalização

- `normalized_login` e `normalized_email` guardam `Trim()` + `ToUpperInvariant()` do valor digitado.
- O cálculo fica em um só lugar, `IdentityPolicy.Normalize` (`Auth.Domain/Policies/`), usado pela entidade `User` e pelos casos de uso nas buscas.
- As colunas `login` e `email` continuam com o valor **como foi digitado**, para exibição e entrega.
- Toda comparação e os índices únicos usam as colunas normalizadas: cadastro (inclusive a checagem de login de conta excluída), login, consulta de usuário (inclusive "é o próprio usuário") e solicitação de reset.
- `Joao@x.com` e `joao@x.com` são a mesma conta. Pontos e `+tag` do e-mail **não** são removidos, porque são regras de provedores específicos.

## Formato do login (cadastros novos)

- de 3 a 32 caracteres;
- só `A-Z`, `a-z`, `0-9`, `.`, `_` e `-`;
- começa e termina com letra ou dígito.

Isso impede logins visualmente idênticos feitos com letras de outros alfabetos ou com caracteres invisíveis. Logins existentes fora do formato continuam válidos: a regra vale só no cadastro.

## Formato do e-mail

No máximo 254 caracteres, sem espaços, exatamente um `@` com as duas partes preenchidas. A validação real continua sendo o e-mail de confirmação.

## Índices

| Índice | Escopo |
|---|---|
| `users_normalized_login_key` | todas as linhas (o login nunca é reaproveitado, ver `docs/auth/0007 - Exclusao de Conta.md`) |
| `users_normalized_email_key` | parcial, `WHERE deleted_at IS NULL` |

## Migration

`V20260928100000__AddNormalizedIdentityToUsers.sql` adiciona as colunas, preenche com `UPPER(LTRIM(RTRIM(...)))`, aplica `NOT NULL`, cria os índices novos e remove `users_login_key` e `users_email_key`.

Se duas contas colidirem depois da normalização, a migration **falha** com mensagem clara e a resolução é manual: contas nunca são unidas automaticamente. O `UPPER()` do SQL Server depende da colação e pode diferir do `ToUpperInvariant()` em caracteres não ASCII de e-mails antigos. Esse risco foi aceito porque os dados atuais são de teste.

## Onde está no código

- `IdentityPolicy` e `User.NormalizedLogin`/`NormalizedEmail` em `Auth.Domain/`.
- Consultas por login e e-mail em `DapperUserRepository`.
