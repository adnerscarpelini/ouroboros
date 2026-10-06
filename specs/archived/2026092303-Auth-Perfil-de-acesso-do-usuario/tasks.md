# 2026092303 - Perfil de acesso do usuario — Tarefas

- [x] **Dev** — Criar enum `UserRole { User, Admin }` em `Auth.Domain` e propriedade `Role` em `User`; `User.Create` atribui sempre `UserRole.User`; `User.Rehydrate` recebe o perfil
- [x] **Dev** — Garantir que `RegisterUserRequest` não aceita perfil; `RegisterUserResponse` passa a devolver `Role`
- [x] **Dev** — Incluir o claim de role no JWT: alterar `IJwtTokenGenerator.Generate`/`JwtTokenGenerator` e as chamadas em `LoginInteractor` e `RefreshAccessTokenInteractor`
- [x] **Dev** — Atualizar a collection Postman (resposta do cadastro com `role`)
- [x] **DBA** — Migration adicionando `role text NOT NULL DEFAULT 'User'` em `auth.users`, com `CHECK (role IN ('User', 'Admin'))`
- [x] **DBA** — Ajustar `DapperUserRepository` (INSERT, UPDATE, todos os SELECTs e mapeamento `UserRow` ↔ `UserRole`)
- [x] **Tester** — Cobrir: cadastro gera `User`; `Rehydrate` restaura o perfil; login emite JWT com o claim de role; refresh mantém o perfil do usuário
- [x] **Tech Writer** — Documentar em `docs/auth/` os perfis existentes, o claim no JWT e o procedimento SQL para promover o primeiro `Admin`
