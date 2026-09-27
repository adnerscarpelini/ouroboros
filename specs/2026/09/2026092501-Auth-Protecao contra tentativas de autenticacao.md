# 2026092501 - Protecao contra tentativas de autenticacao

**Data:** 25/09/2026
**Status:** Concluido
**Servico(s):** auth-service

## Solicitação

Na validação de maturidade do auth-service ("valide nosso projeto de auth, se falta algo além do envio de emails"), apareceu que o login não tem limite de tentativas. Refresh e confirmação de e-mail também não. O usuário aprovou limitar esses endpoints, bloquear temporariamente a conta depois de falhas seguidas e fazer o limite por IP funcionar atrás de proxy.

## Análise

Extensão do `auth-service`. Depende de 2026092508 (o bloqueio usa o login normalizado). Referências: OWASP Authentication Cheat Sheet (força bruta, bloqueio temporário, mensagens genéricas), o lockout do ASP.NET Core Identity (`AccessFailedCount`/`LockoutEnd`) e o Forwarded Headers Middleware do ASP.NET Core.

Decisões:
1. **Duas camadas de proteção:**
   - limite por IP, grosso, que contém varredura a partir de uma origem;
   - bloqueio temporário por conta, fino, que contém força bruta distribuída contra uma conta.
2. **Novos limites por IP**, em janela fixa, no mesmo padrão de `RateLimitingConfiguration`:

   | Política | Limite por IP | Janela |
   |---|---|---|
   | `auth-login` | 20 | 15 min |
   | `auth-refresh` | 60 | 15 min |
   | `email-confirm` | 10 | 15 min |

   O refresh token tem 256 bits, então o limite de refresh só contém abuso de CPU e banco. Os limites de todas as políticas, as existentes e as novas, saem das constantes e vão para opções `RateLimiting:{Politica}:PermitLimit` e `RateLimiting:{Politica}:Window`, validadas no startup. Assim produção e testes (2026092512) ajustam sem recompilar. Os valores padrão continuam os atuais.
3. **Bloqueio por conta, no padrão do ASP.NET Identity.** Colunas `access_failed_count` e `lockout_end` em `auth.users`.
   - 5 falhas de senha seguidas bloqueiam a conta por 15 min e zeram o contador.
   - Um sucesso zera o contador.
   - O bloqueio expira sozinho. Nunca existe bloqueio permanente, que daria ao atacante um jeito de negar acesso à vítima.
4. **Onde uma falha conta.** Em toda verificação de senha de uma conta existente:
   - no login;
   - nas reautenticações: exclusão de conta (2026092306), troca de senha (2026092517) e troca de perfil (2026092520).

   A falha é gravada fora da transação da operação (2026092516, decisão 6).
5. **Conta bloqueada responde igual a senha errada** (`401 Invalid credentials`), sem verificar a senha real. Responder "conta bloqueada" revelaria que o login existe.
6. **Tempo constante.** Toda tentativa executa exatamente uma verificação de hash: a real, ou uma contra um hash fictício quando o login não existe ou a conta está bloqueada. Sem isso, o tempo de resposta revela a existência da conta, e a diferença cresce muito com as 600 mil iterações da 2026092509.
   - **Ressalva:** o cadastro já informa "Login already in use" (decisão da spec 2026092305), então a existência do login não é segredo. As decisões 5 e 6 custam pouco e impedem que o login vire um segundo canal para descobrir contas, mas não fecham sozinhas a enumeração de logins.
7. **Contador atômico no banco.** `SET access_failed_count = access_failed_count + 1 ... RETURNING`, para falhas concorrentes não se perderem. Como fica no PostgreSQL, o bloqueio vale entre réplicas. O limite por IP fica em memória em cada instância, o que é aceitável enquanto há uma réplica. Um armazenamento distribuído só entra quando houver escala horizontal.
8. **IP real atrás de proxy.**
   - `UseForwardedHeaders` roda antes do rate limiter.
   - `X-Forwarded-For` e `X-Forwarded-Proto` só são aceitos de proxies listados em `ForwardedHeaders:KnownProxies` ou `ForwardedHeaders:KnownNetworks`. Lista vazia significa que nenhum proxy é confiável e vale o IP da conexão.
   - `ForwardLimit = 1`.

   Sem isso, atrás de um gateway todos os clientes dividem o mesmo limite.
9. **Logs.** A rejeição registra o login tentado e o IP em `Warning`, nunca a senha nem tokens. O bloqueio de uma conta gera `Warning` com o externalId.

Fora do escopo: CAPTCHA e MFA.

## Tarefas

- [x] **Dev** — Mover os limites de `RateLimitingConfiguration` para opções `RateLimiting:*` validadas no startup, mantendo os valores atuais como padrão
- [x] **Dev** — Criar as políticas `auth-login`, `auth-refresh` e `email-confirm` e aplicá-las aos endpoints de login, refresh e confirmação de e-mail
- [x] **Dev** — Configurar `UseForwardedHeaders` antes do rate limiter, com `KnownProxies`/`KnownNetworks` vindos de configuração e `ForwardLimit = 1`
- [x] **Dev** — Adicionar à entidade `User` o controle de falhas (`AccessFailedCount`, `LockoutEnd`, `IsLockedOut(now)`, registro de falha que bloqueia na 5ª, zeragem no sucesso) e aplicá-lo no login e na reautenticação da exclusão de conta
- [x] **Dev** — Garantir uma verificação de hash por tentativa, com hash fictício para login inexistente e conta bloqueada, e a mesma resposta `401` para login inexistente, senha errada e conta bloqueada
- [x] **Dev** — Atualizar a collection Postman com as respostas `429` dos novos limites
- [x] **DBA** — Migration com `access_failed_count integer NOT NULL DEFAULT 0` e `lockout_end timestamptz` em `auth.users`; persistir e ler os campos no `DapperUserRepository`, com incremento e bloqueio atômicos no próprio `UPDATE`
- [x] **Tester** — Unitários: a 5ª falha bloqueia; o bloqueio expira; o sucesso zera; conta bloqueada responde igual a senha errada sem verificar a senha real; login inexistente passa pelo hash fictício; a reautenticação da exclusão conta como falha
- [x] **Tester** — Integração: `429` nas três políticas novas; IP lido de `X-Forwarded-For` só quando vem de proxy confiável; falhas concorrentes contadas corretamente
- [x] **Tech Writer** — Criar doc em `docs/auth/` sobre proteção contra tentativas (limites, bloqueio, resposta genérica, proxies confiáveis) e atualizar `0003 - Login e Tokens.md`
