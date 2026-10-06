# 2026092509 - Politica e armazenamento de senhas

**Data:** 25/09/2026
**Servico(s):** auth-service

## Solicitação

Na validação de maturidade do auth-service, a política de senha (8 caracteres com regras de composição) e o custo do hash (PBKDF2 com 100 mil iterações) apareceram abaixo das referências atuais. Perguntado sobre o tamanho mínimo, o usuário escolheu **15 caracteres**, conforme o NIST para senha como único fator.

## Análise

Extensão do `auth-service`. Referências: NIST SP 800-63B rev. 4 (verificadores de senha), OWASP Password Storage Cheat Sheet e a API Pwned Passwords do Have I Been Pwned (k-anonymity).

Decisões:
1. **Regra de tamanho (NIST):**
   - mínimo de 15 caracteres, porque não temos MFA;
   - máximo de 128. Acima disso a resposta é `400`, sem truncar;
   - tamanho contado em code points Unicode;
   - qualquer caractere é aceito, inclusive espaço, então frases são bem-vindas.

   **Sem regras de composição.** As exigências de maiúscula, minúscula, dígito e especial saem do `PasswordPolicy`.
2. **Normalização Unicode NFKC** antes de hashear e de verificar, recomendação do NIST. Assim a mesma senha digitada em teclados diferentes gera o mesmo hash.
   - Senhas ASCII não mudam com NFKC.
   - Sem fallback para o valor cru: o projeto não está em produção, então não existe senha antiga com caractere não ASCII a preservar.
3. **Senhas proibidas**, verificadas no cadastro, no reset e na troca (2026092517):
   - **Lista local pequena embutida** (algumas centenas a poucos milhares das senhas mais comuns, de fonte com licença compatível, por exemplo SecLists sob MIT). Barra o óbvio sem rede e é a rede de segurança quando o Pwned Passwords está fora do ar. A comparação não diferencia maiúsculas. Uma lista de 100 mil entradas foi descartada: o Pwned Passwords já cobre as comuns e as vazadas.
   - **Palavras do contexto:** senha que contém o login (quando ele tem 4 ou mais caracteres), a parte local do e-mail ou "ouroboros".
   - **Senhas vazadas, via Pwned Passwords com k-anonymity.** Só os 5 primeiros caracteres hexadecimais do SHA-1 saem do serviço, com o header `Add-Padding: true`. O timeout é de 2 s. Se a API estiver indisponível, o fluxo segue sem essa checagem (fail-open) e registra `Warning`. A disponibilidade do cadastro não fica refém de um terceiro, e a lista local continua valendo.

   Mensagem única: `Password is too common or has appeared in a data breach`. O gateway `IBreachedPasswordChecker` fica na Application, e a implementação HTTP na Infrastructure.
4. **Senhas antigas continuam funcionando**, mesmo abaixo de 15 caracteres. Não há troca forçada nem expiração periódica, que o NIST desaconselha. A política nova vale para toda senha nova: cadastro, reset e troca.
5. **Custo do hash (OWASP):** PBKDF2-HMAC-SHA256 com 600.000 iterações. O formato `{iteracoes}.{salt}.{hash}` continua o mesmo. O tempo de verificação é medido no ambiente alvo, com referência abaixo de 500 ms. Se passar disso, o resultado é registrado e discutido antes de baixar o custo.
6. **Re-hash no login bem-sucedido** quando o número de iterações gravado é menor que o atual. O comando é `UPDATE ... SET password_hash = @novo WHERE external_id = @id AND password_hash = @antigo`. Se a senha tiver sido trocada em paralelo, o update não afeta nada, e isso é o correto. Entra um novo método `IPasswordHasher.NeedsRehash(hash)`.
7. **O hash fictício de tempo constante (2026092501) usa as mesmas 600 mil iterações**, lidas da mesma configuração.
8. O custo extra de CPU é contido pelos limites da 2026092501.
