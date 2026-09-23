# 2026092306 - Exclusao de conta do usuario

**Data:** 23/09/2026
**Status:** Concluido
**Servico(s):** auth-service

## Solicitação

O usuário pediu a exclusão da conta do usuário. A exclusão deve ser lógica, e não física: a conta fica marcada como excluída e inativa. Se a pessoa quiser se cadastrar de novo no futuro, o login pode continuar bloqueado, mas o e-mail deve poder ser reaproveitado. Também pediu que um Admin possa excluir a conta de outro usuário, por exemplo de um admin demitido ou de um cliente banido. Não haverá limpeza nem anonimização posterior: a conta fica apenas marcada como excluída.

## Análise

Extensão do `auth-service`, sem serviço novo. Referências: soft delete com índice único parcial do PostgreSQL, o endpoint `DELETE /users/{id}` do Microsoft Graph, e o OWASP Authentication Cheat Sheet para a reautenticação antes de uma ação destrutiva.

Decisões:
1. **`deleted_at timestamptz` em vez de um campo `deleted` true/false.** Registra se a conta foi excluída e também quando. Na exclusão, `active` passa para `false`.
2. **Reaproveitamento do e-mail por índice único parcial.** `users_email_key` passa a valer só para `deleted_at IS NULL`. O e-mail continua gravado na conta excluída, para auditoria. Descartadas: limpar o e-mail, que apaga o rastro de quem era o dono, e gravar um valor padrão, que é um dado falso em coluna de e-mail.
3. **Login nunca é reaproveitado.** `users_login_key` continua valendo também para contas excluídas. Assim ninguém pega o login de uma conta excluída para se passar pelo dono antigo. Novo cadastro com o login de uma conta excluída devolve `400 Login already in use`.
4. **Conta excluída se comporta como inexistente.** As buscas do repositório ignoram contas excluídas. Consequências:
   - O login devolve `InvalidCredentials`, e não "User is not active".
   - A recuperação de senha devolve a mesma resposta genérica de sempre.
   - A consulta devolve `404`.
   - O e-mail da conta excluída fica livre no cadastro.

   Só a checagem de login do cadastro enxerga as contas excluídas.
5. **Tokens revogados.** A exclusão revoga todos os refresh tokens ativos e invalida os tokens pendentes de confirmação de e-mail e de recuperação de senha.
6. **Cadastro abandonado não remove conta excluída.** A remoção física de cadastro abandonado (spec [2026092305](2026092305-Auth-Seguranca%20do%20cadastro%20de%20usuario.md)) só age sobre contas não excluídas. Uma conta excluída nunca é apagada fisicamente.
7. **Endpoint `DELETE /api/users/{externalId}` com `[Authorize]`.** Permissão por perfil combinada com a regra de dono da conta, aplicada no caso de uso:
   - Um `User` só exclui a própria conta. Pedir a conta de outro usuário devolve `403`, decidido **antes** de consultar o banco, para não revelar se a conta existe.
   - Um `Admin` exclui qualquer conta, inclusive a de outro Admin.
8. **Reautenticação obrigatória.** O body leva a **senha atual de quem está excluindo**, tanto na auto-exclusão quanto na exclusão feita por um Admin. Assim, um access token roubado sozinho não consegue excluir contas. Senha errada devolve `InvalidCredentials`.
9. **Proteção contra perder todos os Admins.** Não é permitido excluir o último Admin ativo: essa tentativa devolve `400`, porque o sistema ficaria sem ninguém para administrar.
10. **Resposta `204 No Content`**, sem dados do usuário. Conta inexistente ou já excluída devolve `404`, e só é consultada depois de passar pela checagem de permissão.
11. **Rate limiting.** O endpoint verifica senha, então precisa de limite contra força bruta. Nova política `user-delete`: 5 tentativas por IP a cada 15 min, janela fixa, no mesmo padrão de `RateLimitingConfiguration`.
12. **Auditoria.** Log `Information` com o externalId de quem excluiu e o da conta excluída. Nunca registrar e-mail nem senha.

Limitações conhecidas (registrar na doc):
- O access token é stateless e continua válido até expirar. O refresh token é revogado, então a conta não ganha uma sessão nova, e os endpoints que carregam o usuário do banco já o tratam como inexistente.
- Os dados pessoais (nome e e-mail) ficam guardados sem prazo, por decisão do usuário. Se surgir uma exigência da LGPD, a anonimização vira uma spec futura.

## Tarefas

- [x] **Dev** — Adicionar `DeletedAt` à entidade `User` (incluir no `Rehydrate`) e o método de domínio `Delete()`: define `DeletedAt`, `Active = false` e `MarkAsUpdated`. Excluir uma conta já excluída lança `DomainException`
- [x] **Dev** — Criar o caso de uso `DeleteUser` em `Auth.Application`. Recebe quem está excluindo (externalId e perfil, vindos do token), a senha dele e o externalId da conta alvo, e executa nesta ordem:
  - Aplica a regra de permissão: o `User` só exclui a si mesmo; se não puder, lança `AccessDeniedException` antes de consultar o banco.
  - Valida a senha de quem está excluindo.
  - Busca a conta alvo; se não existir, lança `UserNotFoundException`.
  - Bloqueia a exclusão do último Admin ativo.
  - Exclui a conta e revoga os refresh tokens e os tokens pendentes.
- [x] **Dev** — Criar o endpoint `DELETE /api/users/{externalId}` (`[Authorize]`) com body `{ password }`. Respostas: `204` em caso de sucesso, `401` com token inválido ou senha errada, `403` sem permissão, `404` quando a conta não existe, `400` para regra de domínio (inclusive a do último Admin). Adicionar log de auditoria
- [x] **Dev** — Criar a política de rate limiting `user-delete` (5 a cada 15 min por IP) e aplicar no endpoint
- [x] **Dev** — Garantir que login, refresh, recuperação de senha, consulta e cadastro tratem a conta excluída conforme a decisão 4 (o cadastro bloqueia o login e libera o e-mail)
- [x] **Dev** — Atualizar a collection Postman com o novo request Delete User, usando Bearer token e senha no body
- [x] **DBA** — Criar a migration com a coluna `deleted_at timestamptz` em `auth.users` e recriar `users_email_key` como índice único parcial (`WHERE deleted_at IS NULL`). `users_login_key` fica como está
- [x] **DBA** — Ajustar o `DapperUserRepository` (persistir e ler `deleted_at`):
  - As buscas por externalId, e-mail, login e login-ou-e-mail filtram `deleted_at IS NULL`.
  - Criar uma checagem de login que inclui as contas excluídas, para o cadastro.
  - Criar a contagem de Admins ativos.
  - `RemoveAsync` nunca remove uma conta excluída.
- [x] **DBA** — Adicionar ao `ITokenRepository` a invalidação dos tokens pendentes de um usuário (reaproveitar `RevokeAllActiveByUserAsync` para os refresh tokens)
- [x] **Tester** — Cobrir: auto-exclusão com sucesso; senha errada; `User` excluindo outra conta → negado sem consultar o repositório; `Admin` excluindo outra conta (inclusive outro Admin); último Admin ativo bloqueado; conta inexistente ou já excluída → 404; tokens revogados; conta excluída tratada como inexistente no login, na recuperação de senha e na consulta; novo cadastro reaproveitando o e-mail da conta excluída; novo cadastro com login de conta excluída → erro; cadastro abandonado não remove conta excluída
- [x] **Tech Writer** — Criar `docs/auth/0007 - Exclusao de Conta.md` com o fluxo, o contrato do endpoint, a regra de acesso, a reautenticação, o reaproveitamento de e-mail versus o bloqueio de login, o rate limiting e as limitações conhecidas. Atualizar `0001 - Confirmacao de Cadastro.md` (e-mail de conta excluída fica livre, login não) e `0006 - Consulta de Usuario.md` (conta excluída devolve 404)
