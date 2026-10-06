# 2026092510 - Autorizacao pelo perfil atual

**Data:** 25/09/2026
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
7. **Correção feita durante a implementação (contagem de Admins da 2026092505).** O teste de concorrência da exclusão de Admins mostrou um deadlock (erro 1205, resposta `500`) em cerca de 1 de cada 60 rodadas: o `UPDLOCK, HOLDLOCK` varria a tabela e seus locks de faixa entravam em ciclo com outras escritas nas mesmas linhas. A contagem passou a tomar um lock de aplicação (`sp_getapplock`, dono `Transaction`), que serializa só as exclusões de Admin sem travar linha nenhuma, e recusa ser chamada fora de uma `IUnitOfWork`. O comportamento para o cliente é o mesmo, com um desfecho a mais, legítimo: quem chega depois do commit da outra exclusão já foi excluído e recebe `401 Invalid access token`. Decidido com o usuário em 06/10/2026: corrigir aqui, sem spec nova.
