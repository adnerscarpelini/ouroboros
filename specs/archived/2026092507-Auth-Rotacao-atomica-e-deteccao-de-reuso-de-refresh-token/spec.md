# 2026092507 - Rotacao atomica e deteccao de reuso de refresh token

**Data:** 25/09/2026
**Servico(s):** auth-service

## Solicitação

Na validação de maturidade do auth-service, a rotação do refresh token apareceu em duas escritas sem transação, e o reuso de um token já revogado era apenas rejeitado. O usuário aprovou tornar a rotação atômica e incluir a detecção de reuso agora, em vez de adiá-la.

## Análise

Depende de 2026092516 (unidade de trabalho) e de 2026092506 (`session_id`). A rotação da spec 2026092204 continua. Referências: RFC 9700 (OAuth 2.0 Security Best Current Practice), seção sobre proteção de refresh tokens, e o "refresh token reuse detection" do Auth0.

Decisões:
1. **Rotação atômica.** Numa transação: revogar o token atual de forma condicional (`revoked_at IS NULL AND expires_at > @now` no SQL) e inserir o sucessor com o mesmo `session_id`. O par novo só é devolvido depois do commit.
2. **Detecção de reuso.** Se o token apresentado existe mas já foi revogado, alguém está usando uma cópia. Pode ser o atacante ou o cliente legítimo depois de o atacante ter rotacionado antes. Nos dois casos:
   - revogar todos os tokens ativos daquela sessão (`session_id`), sem afetar as outras sessões do usuário;
   - registrar `Warning` com o externalId do usuário e o `session_id` (nunca o token);
   - responder `401 Invalid refresh token`, igual aos demais casos.

   Essa revogação roda fora da transação da rotação (2026092516, decisão 6), para não ser desfeita.
3. **Token expirado e não revogado** → só `401`, sem revogar a sessão. Expirar não é sinal de roubo.
4. **Sem janela de tolerância.** Dois refresh simultâneos com o mesmo token derrubam a sessão. Uma janela de tolerância reabriria exatamente a brecha que a detecção fecha. Os clientes devem serializar o refresh, e isso fica documentado.
5. **O novo access token leva o mesmo `sid`** da sessão.
6. **Logout seguido de reuso.** Um token revogado por logout e apresentado de novo também revoga a sessão. É inofensivo, porque a sessão já está encerrada.
7. O endpoint continua público: o refresh token é a credencial e nunca aparece em log nem em URL. Limite `auth-refresh` da 2026092501.
8. **Regra de hints de leitura (skill `ouroboros-dba`, 06/10/2026).** O `DapperRefreshTokenRepository.GetByHashAsync` lê com `WITH (READPAST)`, que pula a linha travada por outra transação. Se o mesmo refresh token chegar em duas requisições ao mesmo tempo, a segunda pode não enxergar a linha enquanto a primeira a rotaciona e receber `401` como token inexistente, **sem disparar a revogação da sessão**. A detecção cobre o reuso sequencial (token já revogado e commitado) e fica sem efeito nessa janela de milissegundos. Se isso não for aceitável, essa leitura específica precisa de outro tratamento (por exemplo, ler sem `READPAST` dentro da transação da rotação), e a decisão volta para o usuário antes de implementar.
