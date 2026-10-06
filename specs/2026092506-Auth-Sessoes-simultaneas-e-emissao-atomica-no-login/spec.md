# 2026092506 - Sessoes simultaneas e emissao atomica no login

**Data:** 25/09/2026
**Servico(s):** auth-service

## Solicitação

Na validação de maturidade do auth-service, o login apareceu revogando as sessões anteriores antes de criar a nova, sem transação. Uma falha deixa o usuário sem sessão nenhuma. Perguntado se "um novo login deve continuar derrubando as sessões anteriores?", o usuário escolheu **várias sessões**, que é o padrão de mercado, com a opção de encerrar todas. Também pediu que o `LastLoginAt`, hoje nunca preenchido, passe a ser gravado.

## Análise

Depende de 2026092516 (unidade de trabalho). Muda a regra "uma sessão ativa por usuário" da spec 2026092203, documentada em `docs/auth/0003 - Login e Tokens.md`. Referências: o modelo de sessões por dispositivo de Google, Microsoft e GitHub e o claim `sid` do OpenID Connect.

Decisões:
1. **Várias sessões simultâneas.** O login deixa de revogar as sessões anteriores. Cada dispositivo tem a sua.
2. **Sessão = família de refresh tokens.** Uma nova coluna `session_id uniqueidentifier` em `auth.refresh_tokens`. O login cria um `session_id` novo, e o refresh herda o da sessão (2026092507). Isso permite encerrar uma sessão inteira, e não só um token.
3. **Claim `sid` no access token**, com o `session_id`. Com ele o serviço sabe qual é a sessão atual: a troca de senha (2026092517) preserva a sessão de quem trocou.
4. **Descartado: limite de sessões ativas por usuário.** O login já tem rate limit (2026092501), o refresh token expira em 7 dias e a limpeza (2026092515) remove os expirados, então o crescimento já é contido. O limite acrescentaria configuração, duas consultas no repositório e uma regra de revogação por um risco que não existe hoje. Se aparecer abuso real, vira spec própria.
5. **Encerrar todas as sessões: `POST /api/auth/logout-all`** com `[Authorize]`. Revoga todos os refresh tokens ativos do `sub` e responde `204`. Os access tokens já emitidos valem até expirar (no máximo 15 min), e essa limitação fica documentada. O `POST /api/auth/logout` atual passa a encerrar só a sessão do refresh token enviado.
6. **Login atômico.** Numa só transação: inserir o refresh token, gravar `last_login_at` e zerar o contador de falhas (2026092501). O JWT e o refresh token só são devolvidos depois do commit.
7. **`LastLoginAt` preenchido** por um método de domínio `User.RegisterLogin(now)`. O valor não entra em nenhuma resposta.
8. **Migration de `session_id`.** A coluna entra nula, cada refresh token existente vira sua própria sessão (`UPDATE ... SET session_id = NEWID()`, um valor por linha) e depois a coluna passa a `NOT NULL`, com índice em `(user_id, session_id)`.

O login continua público, com os limites da 2026092501, erro genérico e senha fora dos logs.
