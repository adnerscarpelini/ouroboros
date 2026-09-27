# 2026092504 - Redefinicao atomica de senha

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

Na validação de maturidade do auth-service, a redefinição de senha apareceu com escritas separadas: uma falha no meio consome o link sem trocar a senha. O usuário aprovou tornar a redefinição uma operação que conclui por inteiro ou é desfeita por inteiro.

## Análise

Depende de 2026092516 (unidade de trabalho). As regras da spec 2026092302 continuam: token de uso único, usuário ativo, nova senha diferente da atual, sem login automático e resposta genérica para token inválido. A política de senha usada aqui é a da 2026092509.

Decisões:
1. **Uma transação** para o consumo condicional do token (`used_at IS NULL AND expires_at > @now` no SQL, o mesmo `TryMarkAsUsedAsync` da 2026092503), a troca do hash, a revogação de todos os refresh tokens do usuário e a zeragem do bloqueio de conta (2026092501).
2. **Ordem mantida.** Primeiro as validações: token pendente, usuário ativo, política de senha e senha diferente da atual. Só depois o consumo. Senha rejeitada não consome o link.
3. **O reset desbloqueia a conta.** Quem conclui o reset provou que controla o e-mail. Zerar `access_failed_count` e `lockout_end` evita que o dono legítimo fique preso depois de um ataque de força bruta.
4. **Limitação que continua:** access tokens já emitidos valem até expirar (no máximo 15 min). As ações que dependem de privilégio passam a ler o perfil do banco (2026092510).

## Tarefas

- [ ] **Dev** — Executar o consumo do token, `ChangePassword`, a zeragem do bloqueio e `RevokeAllActiveByUserAsync` dentro do `IUnitOfWork`, mantendo as validações antes do consumo
- [ ] **DBA** — Garantir que o `TryMarkAsUsedAsync` confira o prazo no SQL (compartilhado com 2026092503)
- [ ] **Tester** — Unitários: senha rejeitada não consome o token; o reset zera o bloqueio. Integração: duas redefinições simultâneas com o mesmo token → só uma troca a senha; falha forçada na revogação → token, senha e sessões intactos
- [ ] **Tech Writer** — Atualizar `docs/auth/0004 - Recuperacao de Senha.md`: remover a limitação "escritas separadas, sem transação" e documentar o desbloqueio da conta
