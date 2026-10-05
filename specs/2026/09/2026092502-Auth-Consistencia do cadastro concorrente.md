# 2026092502 - Consistencia do cadastro concorrente

**Data:** 25/09/2026
**Status:** Concluido
**Servico(s):** auth-service

## Solicitação

Na validação de maturidade do auth-service, o cadastro apareceu como não atômico e sujeito a `500` quando dois cadastros disputam o mesmo login ou e-mail. O usuário aprovou tornar o cadastro uma operação única e tratar essa disputa sem mudar os contratos públicos.

## Análise

Depende de 2026092516 (unidade de trabalho), de 2026092508 (nomes finais dos índices de unicidade) e de 2026092512 (testes com SQL Server real).

Estado atual: o `RegisterUserInteractor` faz até quatro escritas separadas. Remove o dono abandonado do login, remove o do e-mail, insere o usuário e insere o token. Uma falha no meio pode apagar o cadastro abandonado sem criar o novo. Além disso, dois cadastros simultâneos passam juntos pela checagem prévia. O segundo esbarra no índice único, gera `SqlException` com `Number` 2601 e o cliente recebe `500`.

Decisões:
1. **Uma transação** (2026092516) para as remoções de abandonados, a inserção do usuário e a inserção do token. O token de confirmação só é devolvido, e logado enquanto não existe envio de e-mail, depois do commit.
2. **Checagem prévia mais índice único.** A checagem prévia continua, porque dá a resposta certa no caso comum. O índice único é a garantia final.
3. **Tradução da violação na Infrastructure.** O repositório captura `SqlException` com `Number` 2601 (índice único) ou 2627 (constraint única) e decide pelo nome do índice presente na mensagem:
   - `users_normalized_login_key` → `DuplicateLoginException`;
   - `users_normalized_email_key` → `DuplicateEmailException`.

   As duas exceções ficam no Domain e derivam de `DomainException`. Qualquer outra constraint mantém o erro original. `SqlException` não sai da Infrastructure.
4. **Contratos mantidos** (spec 2026092305):
   - Login duplicado → `400 Login already in use`.
   - E-mail duplicado → o mesmo `202` genérico, sem criar nada. O interactor captura `DuplicateEmailException` depois do rollback e devolve a mesma resposta do caso "e-mail ocupado". Não pode existir caminho que diferencie os dois casos.
5. **Remoção de abandonado em corrida.** Duas requisições podem tentar remover o mesmo cadastro abandonado. Um `DELETE` que afeta 0 linhas não é erro. O índice decide quem ganha a inserção.

## Tarefas

- [x] **Dev** — Criar `DuplicateLoginException` e `DuplicateEmailException` em `Auth.Domain/Exceptions`, derivadas de `DomainException`
- [x] **Dev** — Executar todas as escritas do `RegisterUserInteractor` dentro do `IUnitOfWork`; tratar `DuplicateEmailException` como o `202` genérico e `DuplicateLoginException` como `400 Login already in use`
- [x] **DBA** — No `DapperUserRepository.AddAsync`, traduzir os erros 2601 e 2627 do SQL Server pelo nome do índice para as exceções de duplicidade e relançar os demais erros sem alteração
- [x] **Tester** — Unitários: repositório lançando cada exceção de duplicidade → contrato correto; e-mail duplicado não devolve token
- [x] **Tester** — Integração: dois cadastros simultâneos com o mesmo login → um `202` e um `400`; com o mesmo e-mail → dois `202` e uma única conta; falha forçada na inserção do token → rollback (o abandonado continua existindo e o usuário novo não existe)
- [x] **Tech Writer** — Atualizar `docs/auth/0001 - Confirmacao de Cadastro.md` com a garantia transacional e o comportamento em cadastros simultâneos
