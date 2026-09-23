# 2026092201 - Confirmacao de cadastro por email

**Data:** 22/09/2026
**Status:** Concluido
**Servico(s):** auth-service

## Solicitação

Hoje o cadastro de usuário já existe, mas o usuário fica sempre inativo — não há fluxo pra ativá-lo. O usuário quer: ao se cadastrar, gerar um token de validação do cadastro; esse token seria enviado por e-mail (o envio em si fica pra depois, quando existir uma estrutura de mensageria); ao clicar no link recebido, o token é validado na API e o usuário passa a ativo/validado. Foi levantada a ideia de uma tabela de tokens com tipos de token, pra já deixar preparado reaproveitar a mesma estrutura em fluxos futuros (ex.: reset de senha).

## Análise

Extensão natural do `auth-service` — mesma entidade `User`, que já tem os campos `Active` e `EmailConfirmed` prontos no domínio, só sem fluxo que os popule. Introduz uma nova tabela `auth.tokens`, genérica por tipo (`TokenType`), pra ser reaproveitada por outros fluxos de token no futuro sem nova tabela a cada caso. Envio de e-mail fica fora de escopo desta spec — enquanto não existir mensageria, o token gerado é retornado na resposta do endpoint de registro (uso via Swagger/Postman em dev) e logado, com uma nota clara no código de que isso é temporário. Depende da spec [2026092202-Auth-Parametros de configuracao de autenticacao](2026092202-Auth-Parametros%20de%20configuracao%20de%20autenticacao.md) apenas se o tempo de expiração do token for parametrizado por lá — caso contrário, pode ser implementada de forma independente e em paralelo com as specs de login.

## Tarefas

- [x] **Dev** — Criar entidade `Token` (ou `EmailConfirmationToken`) e enum/tipo `TokenType` em `Auth.Domain`, com o tipo `EmailConfirmation` como primeiro valor
- [x] **Dev** — Criar gateway `ITokenRepository` em `Auth.Application`
- [x] **Dev** — Ajustar `RegisterUserInteractor` (ou criar use case dedicado) pra gerar o token de confirmação ao registrar o usuário
- [x] **Dev** — Criar use case `ConfirmEmailUseCase`/`ConfirmEmailInteractor` que valida o token (existe, não expirou, não foi usado) e ativa o usuário (`Active = true`, `EmailConfirmed = true`)
- [x] **Dev** — Criar endpoint de confirmação em `UserController` (ex.: `POST /users/confirm-email`) recebendo o token
- [x] **Dev** — Retornar/logar o token gerado no registro, com nota explícita de que é temporário até existir envio de e-mail
- [x] **DBA** — Migration criando `auth.tokens` (id interno, external id, auditoria, `user_id`, `type`, `token_hash`, `expires_at`, `used_at`)
- [x] **Tester** — Cobrir: geração do token no registro, confirmação com token válido, token expirado, token já usado, token inexistente
- [x] **Tech Writer** — Documentar o fluxo de confirmação de cadastro em `docs/`, deixando explícito que o envio por e-mail ainda não existe
