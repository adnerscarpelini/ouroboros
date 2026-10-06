# 2026092305 - Seguranca do cadastro de usuario

**Data:** 23/09/2026
**Servico(s):** auth-service

## Solicitação

O usuário notou que o cadastro de um novo user devolve na resposta o token de confirmação de e-mail. Isso permite cadastrar um e-mail que não é seu (ex.: `silviosantos@sbt.com.br`) e confirmá-lo sem nunca acessar a caixa de entrada. Pediu que o processo fique igual ao da recuperação de senha, em que o token só é enviado por e-mail. Depois da análise, pediu que as demais fragilidades encontradas no cadastro entrem nesta mesma spec.

## Análise

Extensão do `auth-service`, sem serviço novo. Referência: OWASP Authentication Cheat Sheet e OWASP Forgot Password Cheat Sheet (já usado na spec 2026092301), e o comportamento de IdPs como Auth0/Entra ID.

Fragilidades do cadastro atual:
- **Token na resposta.** `RegisterUserInteractor` devolve o token em `RegisterUserResponse.EmailConfirmationToken` e o `POST /api/users` o expõe. Quem cadastra só declara ser dono do e-mail; a confirmação existe justamente pra provar isso. A premissa da `docs/auth/0004` ("no cadastro quem recebe o token é o próprio dono da conta") está errada.
- **Enumeração de e-mail.** `Login or email already in use` revela se um e-mail tem conta, contrariando o cuidado já tomado na recuperação de senha.
- **E-mail sequestrado.** Um cadastro nunca confirmado de um e-mail alheio bloqueia o dono verdadeiro pra sempre (não existe reenvio nem expiração do cadastro).
- **Sem rate limiting** no `POST /api/users`.

Decisões:
1. **Token fora da resposta.** Só chega pelo e-mail. Enquanto não houver mensageria, fica apenas no log do Seq (temporário, com `TODO`), igual à recuperação de senha.
2. **Resposta genérica.** O `POST /api/users` passa de `201` com dados do usuário pra `202 Accepted` com mensagem fixa (ex.: "If the email is available, a confirmation link will be sent to it."), sem `Location` e sem dados do usuário. E-mail já em uso por conta não abandonada → mesma resposta `202`, nada é criado, `Warning` no log e `TODO` pra notificar o dono da tentativa quando existir e-mail.
3. **Login duplicado continua `400 Login already in use`.** O usuário precisa saber que o login escolhido está ocupado; o OWASP aceita essa exposição, o que se protege é o e-mail. Registrado como limitação conhecida.
4. **Cadastro abandonado.** Conta com e-mail não confirmado e sem token de confirmação pendente (passou das 24h de validade) é abandonada. Um novo cadastro com o mesmo e-mail ou login remove a conta abandonada (e seus tokens) e segue normalmente. Dentro das 24h, o e-mail/login continua reservado. Com isso não é preciso fluxo de reenvio: token expirado → novo cadastro.
5. **Rate limiting.** Política `user-register`, 5 cadastros por IP a cada 15 min, janela fixa, mesmo padrão de `RateLimitingConfiguration`.

Mudança de contrato: o cadastro deixa de devolver o `id`. O `externalId` passa a ser obtido após o login (Search User).

Limitações conhecidas (registrar na doc):
- Diferença de tempo de resposta entre e-mail novo (grava no banco) e e-mail existente ainda permite inferir existência; deve cair com envio de e-mail assíncrono.
- Login continua enumerável (decisão 3).
- **Pre-account takeover:** um atacante pode cadastrar o e-mail da vítima com senha própria; se a vítima clicar no link de confirmação, a conta fica ativa com a senha do atacante. A mitigação de mercado é o fluxo "confirma o e-mail primeiro, define a senha depois" — redesenho fora do escopo desta spec, candidato a spec futura.
