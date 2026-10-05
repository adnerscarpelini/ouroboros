# 2026092506 - Sessoes simultaneas e emissao atomica no login

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

Na validação de maturidade do auth-service, o login apareceu revogando as sessões anteriores antes de criar a nova, sem transação. Uma falha deixa o usuário sem sessão nenhuma. Perguntado se "um novo login deve continuar derrubando as sessões anteriores?", o usuário escolheu **várias sessões**, que é o padrão de mercado, com a opção de encerrar todas. Também pediu que o `LastLoginAt`, hoje nunca preenchido, passe a ser gravado.

## Análise

Depende de 2026092516 (unidade de trabalho). Muda a regra "uma sessão ativa por usuário" da spec 2026092203, documentada em `docs/auth/0003 - Login e Tokens.md`. Referências: o modelo de sessões por dispositivo de Google, Microsoft e GitHub e o claim `sid` do OpenID Connect.

Decisões:
1. **Várias sessões simultâneas.** O login deixa de revogar as sessões anteriores. Cada dispositivo tem a sua.
2. **Sessão = família de refresh tokens.** Uma nova coluna `session_id uniqueidentifier` em `auth.refresh_tokens`. O login cria um `session_id` novo, e o refresh herda o da sessão (2026092507). Isso permite encerrar uma sessão inteira, e não só um token.
3. **Claim `sid` no access token**, com o `session_id`. Com ele o serviço sabe qual é a sessão atual: a troca de senha (2026092517) preserva a sessão de quem trocou.
4. **Limite de 10 sessões ativas por usuário** (`Sessions:MaxActivePerUser`, padrão 10, validado no startup). No 11º login, a sessão ativa mais antiga é revogada na mesma transação. Isso contém o crescimento por script ou abuso. Logins concorrentes **não** são serializados: exceder o limite por uma sessão durante um instante não tem risco.
5. **Encerrar todas as sessões: `POST /api/auth/logout-all`** com `[Authorize]`. Revoga todos os refresh tokens ativos do `sub` e responde `204`. Os access tokens já emitidos valem até expirar (no máximo 15 min), e essa limitação fica documentada. O `POST /api/auth/logout` atual passa a encerrar só a sessão do refresh token enviado.
6. **Login atômico.** Numa só transação: inserir o refresh token, revogar a sessão mais antiga se passar do limite, gravar `last_login_at` e zerar o contador de falhas (2026092501). O JWT e o refresh token só são devolvidos depois do commit.
7. **`LastLoginAt` preenchido** por um método de domínio `User.RegisterLogin(now)`. O valor não entra em nenhuma resposta.
8. **Migration de `session_id`.** Cada refresh token existente vira sua própria sessão (`gen_random_uuid()`). Depois a coluna passa a `NOT NULL`, com índice em `(user_id, session_id)`.

O login continua público, com os limites da 2026092501, erro genérico e senha fora dos logs.

## Tarefas

- [ ] **Dev** — Remover a revogação de sessões anteriores do `LoginInteractor`; adicionar `SessionId` à entidade `RefreshToken`; incluir o claim `sid` no `JwtTokenGenerator`
- [ ] **Dev** — Criar `User.RegisterLogin(now)` e executar dentro do `IUnitOfWork` a gravação do refresh token, a revogação da sessão mais antiga acima do limite, a atualização do usuário (`last_login_at` e contador zerado), devolvendo os tokens só depois do commit
- [ ] **Dev** — Criar a opção validada `Sessions:MaxActivePerUser` (padrão 10)
- [ ] **Dev** — Criar o caso de uso `LogoutAll` e o endpoint `POST /api/auth/logout-all` (`[Authorize]`, `204`); atualizar a collection Postman
- [ ] **DBA** — Migration: `session_id uniqueidentifier` em `auth.refresh_tokens` com backfill, `NOT NULL` e índice `(user_id, session_id)`; persistir e ler `session_id` no `DapperRefreshTokenRepository`
- [ ] **DBA** — Criar no repositório a contagem de sessões ativas por usuário e a revogação da sessão ativa mais antiga
- [ ] **Tester** — Unitários: login não revoga sessões anteriores; 11º login revoga a mais antiga; `LastLoginAt` gravado; contador de falhas zerado; token traz `sid`; logout-all revoga todas
- [ ] **Tester** — Integração: dois logins → duas sessões válidas; falha forçada na atualização do usuário → nenhum refresh token criado; logout de uma sessão não afeta a outra
- [ ] **Tech Writer** — Atualizar `docs/auth/0003 - Login e Tokens.md`: trocar "uma sessão ativa por usuário" por várias sessões, limite, `sid`, logout-all e garantia transacional
