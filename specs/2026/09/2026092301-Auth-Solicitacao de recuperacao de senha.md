# 2026092301 - Solicitacao de recuperacao de senha

**Data:** 23/09/2026
**Status:** Concluido
**Servico(s):** auth-service

## Solicitação

O usuário quer a parte de recuperação de senha dos users. Primeira etapa do fluxo: o usuário informa o login ou o e-mail da conta cuja senha deseja recuperar; um token de validação é gerado e enviado por e-mail com um link. O serviço de e-mail ainda não existe e será feito no futuro — o processo segue igual ao do cadastro de novo usuário. Pediu para seguir os padrões de mercado de segurança no processo.

## Análise

Extensão do `auth-service`, sobre a entidade `User` e a tabela genérica `auth.tokens` criada na spec [2026092201-Auth-Confirmacao de cadastro por email](2026092201-Auth-Confirmacao%20de%20cadastro%20por%20email.md), que já previa reuso para reset de senha. Basta um novo `TokenType.PasswordReset` — a coluna `type` é `text` sem constraint, então **não há migration**. `ITokenGenerator` (geração + hash) e `ITokenRepository` já existem.

Decisões de segurança (OWASP Forgot Password Cheat Sheet), aprovadas pelo usuário:

- **Token nunca volta na resposta.** Diferente do cadastro (onde quem recebe é o próprio dono da conta), aqui qualquer pessoa poderia informar o login de outra e receber o token. Até existir envio de e-mail, o token só é **logado**, com TODO explícito de que é temporário.
- **Sem enumeração de usuários.** A resposta é sempre `202 Accepted` com mensagem genérica, exista ou não o login/e-mail.
- **Usuário inativo / e-mail não confirmado é ignorado silenciosamente** (mesmo `202`, sem token) — o reset não pode virar atalho para ativar conta.
- **Token forte, armazenado só como hash, uso único, expiração curta de 1h** (contra 24h da confirmação de e-mail).
- **Só o link mais recente vale:** nova solicitação invalida os tokens `PasswordReset` pendentes anteriores do usuário.
- **Rate limiting** no endpoint, para conter abuso (spam de solicitações e varredura de logins).
- A conta não é alterada nesta etapa — a senha atual continua válida até a redefinição ser concluída (spec 2026092302).

Risco: o log do token é sensível — deve continuar restrito a desenvolvimento e ser removido quando houver envio de e-mail.

## Tarefas

- [x] **Dev** — Adicionar `PasswordReset` ao enum `TokenType` em `Auth.Domain`
- [x] **Dev** — Criar `IUserRepository.GetByLoginOrEmailAsync(string loginOrEmail)` em `Auth.Application`
- [x] **Dev** — Criar `ITokenRepository.InvalidatePendingByUserAsync(Guid userExternalId, TokenType type, DateTimeOffset invalidatedAt)` para invalidar tokens pendentes (não usados e não expirados) do usuário
- [x] **Dev** — Criar use case `RequestPasswordResetUseCase`/`RequestPasswordResetInteractor`: busca o usuário por login ou e-mail; se não existir ou estiver inativo/sem e-mail confirmado, termina sem erro e sem token; caso contrário, invalida os tokens de reset pendentes, gera um novo token de 1h e retorna o token em claro apenas para o controller logar
- [x] **Dev** — Criar endpoint `POST /api/users/password-reset/request` em `UserController`: resposta sempre `202 Accepted` com mensagem genérica, token nunca no corpo, apenas logado com TODO temporário (mesmo padrão do cadastro)
- [x] **Dev** — Configurar rate limiting (middleware nativo do ASP.NET Core) no endpoint de solicitação
- [x] **DBA** — Implementar no Dapper `GetByLoginOrEmailAsync` e `InvalidatePendingByUserAsync` (sem migration — `type` já é `text`)
- [x] **Tester** — Cobrir: solicitação por login, solicitação por e-mail, usuário inexistente (sem erro, sem token), usuário inativo/sem e-mail confirmado (sem erro, sem token), invalidação dos tokens de reset anteriores, token gerado com tipo `PasswordReset` e expiração de 1h
