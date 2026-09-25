# 2026092510 - Invalidacao de JWT em eventos sensiveis

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

O usuário aprovou especificar a revogação efetiva do acesso após redefinição de senha, exclusão da conta ou retirada de privilégio.

## Análise

Hoje o JWT permanece válido até expirar mesmo depois desses eventos; as rotas protegidas usam claims sem consultar o estado atual do usuário. Adotar uma versão de segurança por usuário conferida na validação do JWT para eventos que exigem encerramento imediato, mantendo `exp`, `iss`, `aud` e algoritmo validados. A promoção e o rebaixamento de Admin hoje são feitos por SQL manual; a versão precisa avançar também nesse caminho, ou a mudança de perfil deve passar a ser feita exclusivamente por operação controlada. Se outros serviços passarem a validar o JWT, eles precisarão aplicar a mesma política ou depender de introspecção; não prometer revogação global antes disso. A checagem não deve expor dados pessoais, gerar enumeração nem permitir que um papel antigo continue autorizando ações sensíveis.

## Tarefas

- [ ] **Dev** — Emitir e validar uma versão de segurança no JWT e avançá-la após reset, exclusão e rebaixamento de perfil; negar acesso com versão antiga.
- [ ] **DBA** — Persistir e atualizar a versão de forma atômica com cada evento, inclusive alterações manuais de perfil, considerando concorrência com emissão e refresh de tokens.
- [ ] **Tester** — Cobrir JWT antigo e novo após cada evento, concorrência com emissão e autorização de usuário/Admin nos endpoints protegidos.
- [ ] **Tech Writer** — Atualizar `docs/auth/0003 - Login e Tokens.md`, `0005 - Perfis de Acesso.md` e `0007 - Exclusao de Conta.md` com a semântica de revogação.
