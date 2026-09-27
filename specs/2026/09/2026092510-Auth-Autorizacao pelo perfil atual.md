# 2026092510 - Autorizacao pelo perfil atual

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

A versão anterior desta spec propunha uma "versão de segurança" conferida no JWT a cada requisição. Na revisão, isso foi considerado caro para o benefício. O usuário aprovou reduzir o escopo: as ações que dependem de privilégio passam a decidir pelo perfil gravado no banco, e não pelo claim do token.

## Análise

Extensão do `auth-service`. Hoje a consulta de usuário (spec 2026092304) e a exclusão de conta (spec 2026092306) decidem "é Admin?" pelo claim `role`, que vale até o token expirar (no máximo 15 min). Com a troca de perfil (2026092520), um Admin rebaixado continuaria agindo como Admin durante esse intervalo.

Decisões:
1. **Ações privilegiadas leem o perfil do banco.** Isso vale para consultar conta alheia, excluir conta alheia e trocar perfil (2026092520). O claim `role` continua no token, para outros serviços e para decisões não sensíveis.
2. **A checagem "é o próprio usuário" usa os dados atuais do solicitante no banco** (login e e-mail normalizados, 2026092508), e não os claims `unique_name`/`email`.
3. **Solicitante inexistente ou excluído → `401 Invalid access token`.** Com isso, nos endpoints do auth-service, uma conta excluída perde o acesso na hora, mesmo com o access token ainda válido.
4. **"Negar antes de consultar o alvo" continua.** Lê-se o solicitante, nunca o alvo, antes de decidir. Negar continua sem revelar se a conta pedida existe.
5. **Descartado: versão de segurança por requisição** (a proposta anterior). Ela
   - acrescenta uma leitura de banco a toda requisição;
   - exige incrementar a versão até em alteração manual por SQL;
   - só protege o auth-service, porque os outros serviços validam o JWT sozinhos (2026092518).

   A janela de 15 min é o padrão aceito para access tokens curtos. O que não pode esperar 15 min, que é privilégio, passa a ler o banco.
6. **Limitação documentada:** nos outros serviços, o `role` e o acesso continuam valendo até o `exp` do token.

## Tarefas

- [ ] **Dev** — `GetUserInteractor`: carregar o solicitante pelo `RequesterId`, decidir "é Admin?" e "é o próprio usuário?" pelos dados dele no banco e responder `401` se ele não existir; remover `RequesterLogin`, `RequesterEmail` e `RequesterRole` do `GetUserRequest`
- [ ] **Dev** — `DeleteUserInteractor`: usar o `Role` do solicitante já carregado em vez do claim; remover `RequesterRole` do `DeleteUserRequest`; ajustar o `UserController`
- [ ] **Tester** — Unitários: token com `role=Admin` e solicitante `User` no banco → negado na consulta e na exclusão de conta alheia; solicitante excluído → `401`; negação sem consultar o alvo
- [ ] **Tech Writer** — Atualizar `docs/auth/0005 - Perfis de Acesso.md`, `0006 - Consulta de Usuario.md` e `0007 - Exclusao de Conta.md`: privilégio decidido pelo banco e limitação em outros serviços
