# 2026092503 - Confirmacao atomica de cadastro

**Data:** 25/09/2026
**Servico(s):** auth-service

## Solicitação

Na validação de maturidade do auth-service, a confirmação de e-mail apareceu sem consumo único garantido no banco e sem transação. O usuário aprovou alinhar esse fluxo com a regra dos outros tokens.

## Análise

**O risco é baixo e a spec é pequena de propósito.** Duas confirmações simultâneas chegam ao mesmo resultado, a conta ativa. Uma falha parcial não dá acesso novo a ninguém. Ela entra porque aproveita a unidade de trabalho (2026092516) e o consumo condicional que o reset também passa a usar (2026092504). Assim, os três fluxos de token seguem a mesma regra.

Decisões:
1. **Consumo condicional no banco.** O comando é `UPDATE ... SET used_at = @now WHERE external_id = @id AND used_at IS NULL AND expires_at > @now`. Se afetar 0 linhas, o token é inválido. É o mesmo `TryMarkAsUsedAsync` do reset, que agora também confere o prazo no SQL.
2. **Consumo do token e ativação do usuário na mesma transação.**
3. **Mensagem única, `400 Invalid or expired confirmation token`,** para token vazio, inexistente, de outro tipo, expirado, já usado, ou de conta inexistente ou excluída. Hoje o domínio vaza a diferença ("Token has already been used", "Token has expired").
4. O endpoint continua público, com o limite `email-confirm` da spec 2026092501.
