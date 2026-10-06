# 2026092301 - Solicitacao de recuperacao de senha

**Data:** 23/09/2026
**Servico(s):** auth-service

## Solicitação

O usuário quer a parte de recuperação de senha dos users. Primeira etapa do fluxo: o usuário informa o login ou o e-mail da conta cuja senha deseja recuperar; um token de validação é gerado e enviado por e-mail com um link. O serviço de e-mail ainda não existe e será feito no futuro — o processo segue igual ao do cadastro de novo usuário. Pediu para seguir os padrões de mercado de segurança no processo.

## Análise

Extensão do `auth-service`, sobre a entidade `User` e a tabela genérica `auth.tokens` criada na spec 2026092201, que já previa reuso para reset de senha. Basta um novo `TokenType.PasswordReset` — a coluna `type` é `text` sem constraint, então **não há migration**. `ITokenGenerator` (geração + hash) e `ITokenRepository` já existem.

Decisões de segurança (OWASP Forgot Password Cheat Sheet), aprovadas pelo usuário:

- **Token nunca volta na resposta.** Diferente do cadastro (onde quem recebe é o próprio dono da conta), aqui qualquer pessoa poderia informar o login de outra e receber o token. Até existir envio de e-mail, o token só é **logado**, com TODO explícito de que é temporário.
- **Sem enumeração de usuários.** A resposta é sempre `202 Accepted` com mensagem genérica, exista ou não o login/e-mail.
- **Usuário inativo / e-mail não confirmado é ignorado silenciosamente** (mesmo `202`, sem token) — o reset não pode virar atalho para ativar conta.
- **Token forte, armazenado só como hash, uso único, expiração curta de 1h** (contra 24h da confirmação de e-mail).
- **Só o link mais recente vale:** nova solicitação invalida os tokens `PasswordReset` pendentes anteriores do usuário.
- **Rate limiting** no endpoint, para conter abuso (spam de solicitações e varredura de logins).
- A conta não é alterada nesta etapa — a senha atual continua válida até a redefinição ser concluída (spec 2026092302).

Risco: o log do token é sensível — deve continuar restrito a desenvolvimento e ser removido quando houver envio de e-mail.
