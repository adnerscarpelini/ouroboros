# 2026092509 - Politica e armazenamento de senhas — Tarefas

- [x] **Dev** — Reescrever o `PasswordPolicy`: 15 a 128 code points, sem composição, palavras de contexto (login e parte local do e-mail recebidos como parâmetro) e mensagens novas
- [x] **Dev** — Embutir a lista local pequena de senhas comuns e criar o gateway `IBreachedPasswordChecker` com a implementação Pwned Passwords (k-anonymity, padding, timeout de 2 s, fail-open com `Warning`)
- [x] **Dev** — Aplicar a nova política no cadastro e no reset
- [x] **Dev** — No `Pbkdf2PasswordHasher`, usar 600.000 iterações, normalizar com NFKC e criar `NeedsRehash`; no login bem-sucedido, re-hashear quando necessário
- [x] **DBA** — Criar no `DapperUserRepository` a atualização condicional do hash (`WHERE password_hash = @antigo`)
- [x] **Tester** — Unitários: limites 14/15/128/129; frase com espaços e acentos aceita; composição não é mais exigida; senha comum, senha com o login e senha vazada rejeitadas; falha da API externa não bloqueia; hash antigo de 100 mil verifica e sinaliza re-hash; NFKC aplicado antes do hash
- [x] **Tester** — Integração: re-hash no login não sobrescreve troca de senha concorrente. Medir e registrar o tempo de uma verificação com 600 mil iterações
- [x] **Tech Writer** — Atualizar `docs/auth/0001 - Confirmacao de Cadastro.md` e `0004 - Recuperacao de Senha.md` com a nova política; documentar o custo do hash e a checagem de senhas vazadas
