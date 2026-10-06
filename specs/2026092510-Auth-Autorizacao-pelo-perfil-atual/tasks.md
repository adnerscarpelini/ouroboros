# 2026092510 - Autorizacao pelo perfil atual — Tarefas

- [ ] **Dev** — `GetUserInteractor`: carregar o solicitante pelo `RequesterId`, decidir "é Admin?" e "é o próprio usuário?" pelos dados dele no banco e responder `401` se ele não existir; remover `RequesterLogin`, `RequesterEmail` e `RequesterRole` do `GetUserRequest`
- [ ] **Dev** — `DeleteUserInteractor`: usar o `Role` do solicitante já carregado em vez do claim; remover `RequesterRole` do `DeleteUserRequest`; ajustar o `UserController`
- [ ] **Tester** — Unitários: token com `role=Admin` e solicitante `User` no banco → negado na consulta e na exclusão de conta alheia; solicitante excluído → `401`; negação sem consultar o alvo
- [ ] **Tech Writer** — Atualizar `docs/auth/0005 - Perfis de Acesso.md`, `0006 - Consulta de Usuario.md` e `0007 - Exclusao de Conta.md`: privilégio decidido pelo banco e limitação em outros serviços
