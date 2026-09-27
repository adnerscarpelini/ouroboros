# 2026092508 - Politica de identidade de login e email

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

Na validação de maturidade do auth-service, os índices de login e e-mail apareceram diferenciando maiúsculas de minúsculas: `Joao@x.com` e `joao@x.com` viram duas contas. O usuário aprovou uma política única de comparação, com decisões fechadas (a versão anterior deixava a política da parte local do e-mail em aberto) e com a restrição de caracteres do login.

## Análise

Extensão do `auth-service`. Referências: `NormalizedUserName` e `NormalizedEmail` do ASP.NET Core Identity, e OWASP Input Validation Cheat Sheet (allowlist). Deve ser implementada antes da 2026092502, que usa os nomes dos índices definidos aqui.

Decisões:
1. **Colunas normalizadas, no padrão do ASP.NET Identity.** `normalized_login` e `normalized_email` guardam `Trim()` + `ToUpperInvariant()`. O cálculo fica no domínio (`User`), e essas colunas são usadas em toda comparação e nos índices únicos. As colunas `login` e `email` continuam guardando o valor como foi digitado, para exibição e entrega.
2. **E-mail comparado inteiro sem diferenciar maiúsculas, inclusive a parte local.** A RFC 5321 permite parte local sensível a maiúsculas, mas nenhum provedor relevante usa isso. Google, Microsoft e o ASP.NET Identity tratam como iguais. Pontos e `+tag` **não** são removidos, porque são regras de provedores específicos.
3. **Formato do login em cadastros novos (allowlist):**
   - de 3 a 32 caracteres;
   - só `A-Z`, `a-z`, `0-9`, `.`, `_` e `-`;
   - começa e termina com letra ou dígito.

   Isso impede logins visualmente idênticos feitos com letras de outros alfabetos ou com caracteres invisíveis. Logins existentes fora do formato continuam válidos, sem exigir migração do usuário.
4. **Formato do e-mail:** no máximo 254 caracteres, sem espaços, exatamente um `@` com as duas partes preenchidas. A validação real continua sendo o e-mail de confirmação.
5. **Índices:**
   - `users_normalized_login_key` vale para todas as linhas, porque o login nunca é reaproveitado (spec 2026092306);
   - `users_normalized_email_key` é parcial, `WHERE deleted_at IS NULL`.

   Os índices antigos `users_login_key` e `users_email_key` são removidos.
6. **Migration:** adiciona as colunas, preenche com `upper(btrim(...))`, aplica `NOT NULL` e cria os índices. Se houver colisão, a migration falha com mensagem clara e a resolução é manual. O banco ainda é de desenvolvimento, e contas nunca são unidas automaticamente. O `upper()` do PostgreSQL pode diferir do `ToUpperInvariant()` em caracteres não ASCII de e-mails antigos. Esse risco é aceito, porque os dados atuais são de teste.
7. **Todo ponto de busca usa o valor normalizado:**
   - o cadastro, inclusive a checagem de login de conta excluída;
   - o login;
   - a consulta de usuário, inclusive a comparação "é o próprio usuário" da spec 2026092304;
   - a solicitação de reset.
8. As respostas genéricas para e-mail e as regras de ownership continuam como estão.

## Tarefas

- [ ] **Dev** — Adicionar `NormalizedLogin` e `NormalizedEmail` à entidade `User`, calculados no `Create` e no `Rehydrate`, e um normalizador único no domínio usado também pelos casos de uso nas buscas
- [ ] **Dev** — Validar o formato do login (allowlist) e do e-mail no `User.Create`, com mensagens de erro claras
- [ ] **DBA** — Migration com as colunas normalizadas, backfill, `NOT NULL`, os índices `users_normalized_login_key` (total) e `users_normalized_email_key` (parcial) e remoção dos índices antigos; falhar com mensagem clara em caso de colisão
- [ ] **DBA** — Trocar todas as consultas do `DapperUserRepository` por login e e-mail para as colunas normalizadas e persistir essas colunas no insert e no update
- [ ] **Tester** — Unitários: normalização (maiúsculas, espaços nas pontas); login fora da allowlist é rejeitado; e-mail acima de 254 ou com espaço é rejeitado. Integração: cadastro com e-mail em outra caixa cai no `202` genérico; login em outra caixa autentica; reset e consulta encontram a conta em qualquer caixa
- [ ] **Tech Writer** — Criar doc em `docs/auth/` com a política de identidade (normalização, formato do login, e-mail sem distinção de maiúsculas) e atualizar `0001 - Confirmacao de Cadastro.md`
