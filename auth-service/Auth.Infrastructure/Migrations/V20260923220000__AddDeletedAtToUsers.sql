-- Exclusao logica de usuario. O e-mail so precisa ser unico entre contas nao excluidas, pra poder ser reaproveitado
-- num novo cadastro; o login continua unico em todas as linhas e nunca e reaproveitado.
ALTER TABLE auth.users
    ADD deleted_at datetimeoffset NULL;
GO

DROP INDEX users_email_key ON auth.users;
GO

CREATE UNIQUE INDEX users_email_key ON auth.users (email) WHERE deleted_at IS NULL;
