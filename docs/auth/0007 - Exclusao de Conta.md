# Exclusão de Conta

A exclusão é **lógica**: a linha continua em `auth.users` com `deleted_at` preenchido e `active = false`. Nenhuma conta excluída é apagada fisicamente, e os dados não são anonimizados depois.

## Endpoint

```
DELETE /api/users/3b8f55c5-155c-41a7-9949-e5b7d0d83ffd
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
{ "password": "S3cret!1" }

204 No Content
```

- `externalId` na URL: a conta a excluir.
- `password` no body: a senha atual **de quem está logado**, não a da conta excluída.
- A resposta não traz dados do usuário.

## Regra de acesso

| Perfil do solicitante | Pode excluir |
|---|---|
| `Admin` | Qualquer conta, inclusive a de outro `Admin` |
| `User` | Só a própria |

- Do token vem só o `sub`. **O perfil do solicitante é lido do banco**, não do claim `role`: um Admin rebaixado não exclui mais ninguém na próxima requisição, mesmo com o token ainda válido.
- `User` pedindo outra conta → `403`, decidido lendo só o solicitante e **antes** de consultar o alvo, então a resposta não revela se a conta existe.
- **Solicitante inexistente ou excluído → `401 Invalid access token`**, mesmo com o token dentro do prazo.
- **Não é permitido excluir o último `Admin` ativo** (`400`), pra que o sistema nunca fique sem administrador.

## Reautenticação

Uma sessão aberta não basta: toda exclusão pede a senha atual de quem está fazendo a ação (OWASP Authentication Cheat Sheet).

- Vale na auto-exclusão e quando um `Admin` exclui outra conta. Nesse caso, a senha pedida é a do `Admin`.
- Um access token roubado sozinho não exclui nenhuma conta.
- Senha errada ou vazia → `401 Invalid login or password`. Nada é alterado.

## O que acontece na exclusão

Os passos 1 a 3 rodam numa **única transação**: ou acontecem todos, ou nenhum.

1. `deleted_at` recebe o instante atual, `active` vira `false` e `updated_at` é atualizado.
2. Todos os refresh tokens ativos da conta são revogados.
3. Os tokens pendentes de confirmação de e-mail e de recuperação de senha são invalidados (expiração antecipada).
4. Fica um log `Information` no Seq com o `externalId` da conta excluída e o de quem excluiu. E-mail e senha nunca vão pro log.

## Atomicidade e último Admin

- **Tudo ou nada.** A exclusão lógica, a revogação das sessões e a invalidação dos dois tipos de token rodam dentro de uma `IUnitOfWork` (ver `docs/project/0001 - Arquitetura.md`). Se qualquer passo falhar, a conta continua ativa, com as sessões e os tokens intactos, e a resposta é `500`. A mesma chamada funciona numa nova tentativa.
- **Antes da transação:** a autorização, a reautenticação e a busca do alvo. Negar acesso, errar a senha ou apontar uma conta inexistente nunca abre transação.
- **Falha de senha fora da transação.** A falha é gravada em autocommit, então conta para o bloqueio de conta (ver `docs/auth/0008 - Protecao contra Tentativas de Autenticacao.md`) e não é desfeita por nenhum rollback.
- **Alvo relido dentro da transação.** Como o update regrava a linha toda, o alvo é lido de novo na transação. Uma exclusão ou troca de senha confirmada entre a primeira leitura e a escrita não é sobrescrita. Se o alvo sumiu nesse meio tempo, a resposta é `404`.
- **Último Admin sob concorrência.** Quando o alvo é um Admin ativo, a contagem de Admins ativos (`CountActiveAdminsForUpdateAsync`) toma antes um lock de aplicação exclusivo (`sp_getapplock`, dono `Transaction`, recurso `auth.admin-count`), que dura até o commit ou rollback. Dois Admins que se excluem ao mesmo tempo ficam em fila: o primeiro conclui, e o segundo ou já enxerga um Admin só e recebe `400 The last active admin cannot be deleted`, ou, se chegou depois do commit do primeiro, já foi excluído e recebe `401 Invalid access token`. Nunca saem dois `204`. Quando o alvo não é Admin, nenhum lock é tomado.
- **Por que lock de aplicação e não `UPDLOCK, HOLDLOCK`.** A primeira versão travava as linhas dos Admins com `UPDLOCK, HOLDLOCK`. Como a leitura varre a tabela, os locks de faixa entravam em ciclo com outras escritas nas mesmas linhas, e a transação perdedora recebia deadlock (erro 1205, resposta `500`) em cerca de 1 de cada 60 rodadas do teste de concorrência. O lock de aplicação não trava linha nenhuma, então não forma ciclo. O método recusa ser chamado fora de uma transação (`InvalidOperationException`), porque o lock soltaria na hora.

## Conta excluída se comporta como inexistente

As buscas do `DapperUserRepository` filtram `deleted_at IS NULL`. Pra aplicação, a conta deixou de existir:

| Fluxo | Resultado |
|---|---|
| Login | `401 Invalid login or password`, e não `User is not active` |
| Refresh de token | `401 Invalid refresh token` |
| Recuperação de senha | mesma resposta `202` genérica de sempre |
| Consulta de usuário | `404 User not found` |
| Nova exclusão da mesma conta | `404 User not found` |

## E-mail livre, login reservado

- **E-mail:** pode ser usado num novo cadastro. O índice `users_email_key` é **parcial** (`WHERE deleted_at IS NULL`): o e-mail só precisa ser único entre contas não excluídas. Ele continua gravado na conta excluída, pra auditoria.
- **Login:** nunca é reaproveitado. `users_login_key` vale pra todas as linhas, e um novo cadastro com o login de uma conta excluída recebe `400 Login already in use`. Assim ninguém pega o login de uma conta excluída pra se passar pelo dono antigo.
- A limpeza de cadastro abandonado (ver `docs/auth/0001 - Confirmacao de Cadastro.md`) nunca remove uma conta excluída.

Opções descartadas pra liberar o e-mail:

- **Apagar o e-mail da conta excluída:** perde o rastro de quem era o dono.
- **Trocar por um valor padrão:** grava um dado falso numa coluna de e-mail.

## Erros

Todos com corpo `{"error": "..."}`, exceto o `401` de token e o `429`.

| Situação | Status | Mensagem |
|---|---|---|
| Sem token, token inválido ou expirado | `401` | *(sem corpo)* |
| Solicitante inexistente ou excluído, com token válido | `401` | `Invalid access token` |
| Senha errada ou vazia | `401` | `Invalid login or password` |
| `User` excluindo outra conta | `403` | `Access denied` |
| Conta não existe ou já foi excluída | `404` | `User not found` |
| Exclusão do último `Admin` ativo | `400` | `The last active admin cannot be deleted` |
| Limite de requisições excedido | `429` | *(sem corpo)* |

`400`, `401`, `403` e `404` são logados em `Warning` no Seq, com o `externalId` do solicitante.

## Limite de requisições

Política `user-delete`: 5 tentativas por IP a cada 15 min, janela fixa. O endpoint verifica senha, então o limite contém tentativas de adivinhar a senha a partir de um token roubado.

## Limitações conhecidas

- **Access token continua válido até expirar.** Ele é stateless. O refresh token é revogado, então a conta não ganha uma sessão nova, e os endpoints que carregam o usuário do banco já o tratam como inexistente.
- **Dados pessoais guardados sem prazo.** Nome e e-mail ficam na conta excluída. Se a LGPD exigir, a anonimização vira uma spec nova.
- **Dois Admins excluindo a mesma conta ao mesmo tempo.** Podem os dois receber `204`: o estado final é o mesmo e é idempotente, mas o segundo não vê o `404`. Fechar isso exigiria ler o alvo com lock de linha, o que não foi feito.
- **Troca de senha no meio da exclusão.** Entre a releitura do alvo na transação e o update existe uma janela de milissegundos em que uma redefinição de senha confirmada poderia ser sobrescrita. Não tem teste.

## Onde está no código

- Domínio: `User.Delete()` e `User.DeletedAt` em `Auth.Domain/Entities/User.cs`.
- Caso de uso: `Auth.Application/UseCases/DeleteUser/`.
- Endpoint: `UserController.Delete` em `Auth.Api/Controllers/`. Body: `Auth.Api/Models/DeleteUserBody.cs`.
- Rate limiting: `Auth.Api/Configuration/RateLimitingConfiguration.cs`.
- Persistência: filtros e os métodos `ExistsDeletedByLoginAsync` e `CountActiveAdminsForUpdateAsync` (com `UPDLOCK, HOLDLOCK`) em `DapperUserRepository`.
- Transação: `SqlUnitOfWork` em `Auth.Infrastructure/Persistence/`, usada por `DeleteUserInteractor`.
- Migration: `V20260923220000__AddDeletedAtToUsers.sql`.
