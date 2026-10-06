# 2026092508 - Politica de identidade de login e email — Tarefas

- [x] **Dev** — Adicionar `NormalizedLogin` e `NormalizedEmail` à entidade `User`, calculados no `Create` e no `Rehydrate`, e um normalizador único no domínio usado também pelos casos de uso nas buscas
- [x] **Dev** — Validar o formato do login (allowlist) e do e-mail no `User.Create`, com mensagens de erro claras
- [x] **DBA** — Migration com as colunas normalizadas, backfill, `NOT NULL`, os índices `users_normalized_login_key` (total) e `users_normalized_email_key` (parcial) e remoção dos índices antigos; falhar com mensagem clara em caso de colisão
- [x] **DBA** — Trocar todas as consultas do `DapperUserRepository` por login e e-mail para as colunas normalizadas e persistir essas colunas no insert e no update
- [x] **Tester** — Unitários: normalização (maiúsculas, espaços nas pontas); login fora da allowlist é rejeitado; e-mail acima de 254 ou com espaço é rejeitado. Integração: cadastro com e-mail em outra caixa cai no `202` genérico; login em outra caixa autentica; reset e consulta encontram a conta em qualquer caixa
- [x] **Tech Writer** — Criar doc em `docs/auth/` com a política de identidade (normalização, formato do login, e-mail sem distinção de maiúsculas) e atualizar `0001 - Confirmacao de Cadastro.md`
