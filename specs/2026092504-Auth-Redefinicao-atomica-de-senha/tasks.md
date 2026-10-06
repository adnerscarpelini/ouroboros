# 2026092504 - Redefinicao atomica de senha — Tarefas

- [ ] **Dev** — Executar o consumo do token, `ChangePassword`, a zeragem do bloqueio e `RevokeAllActiveByUserAsync` dentro do `IUnitOfWork`, mantendo as validações antes do consumo
- [ ] **DBA** — Garantir que o `TryMarkAsUsedAsync` confira o prazo no SQL (compartilhado com 2026092503)
- [ ] **Tester** — Unitários: senha rejeitada não consome o token; o reset zera o bloqueio. Integração: duas redefinições simultâneas com o mesmo token → só uma troca a senha; falha forçada na revogação → token, senha e sessões intactos
- [ ] **Tech Writer** — Atualizar `docs/auth/0004 - Recuperacao de Senha.md`: remover a limitação "escritas separadas, sem transação" e documentar o desbloqueio da conta
