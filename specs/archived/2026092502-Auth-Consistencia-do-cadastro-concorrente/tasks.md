# 2026092502 - Consistencia do cadastro concorrente — Tarefas

- [x] **Dev** — Criar `DuplicateLoginException` e `DuplicateEmailException` em `Auth.Domain/Exceptions`, derivadas de `DomainException`
- [x] **Dev** — Executar todas as escritas do `RegisterUserInteractor` dentro do `IUnitOfWork`; tratar `DuplicateEmailException` como o `202` genérico e `DuplicateLoginException` como `400 Login already in use`
- [x] **DBA** — No `DapperUserRepository.AddAsync`, traduzir os erros 2601 e 2627 do SQL Server pelo nome do índice para as exceções de duplicidade e relançar os demais erros sem alteração
- [x] **Tester** — Unitários: repositório lançando cada exceção de duplicidade → contrato correto; e-mail duplicado não devolve token
- [x] **Tester** — Integração: dois cadastros simultâneos com o mesmo login → um `202` e um `400`; com o mesmo e-mail → dois `202` e uma única conta; falha forçada na inserção do token → rollback (o abandonado continua existindo e o usuário novo não existe)
- [x] **Tech Writer** — Atualizar `docs/auth/0001 - Confirmacao de Cadastro.md` com a garantia transacional e o comportamento em cadastros simultâneos
