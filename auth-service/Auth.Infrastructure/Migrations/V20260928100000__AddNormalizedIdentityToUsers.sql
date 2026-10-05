-- Politica de identidade (spec 2026092508): login e e-mail passam a ser comparados pelas colunas normalizadas
-- (Trim + maiusculas), no padrao NormalizedUserName/NormalizedEmail do ASP.NET Identity.
-- As colunas login e email continuam guardando o valor como foi digitado.
ALTER TABLE auth.users
    ADD normalized_login nvarchar(256) COLLATE Latin1_General_100_CS_AS NULL,
        normalized_email nvarchar(256) COLLATE Latin1_General_100_CS_AS NULL;
GO

UPDATE auth.users
SET
    normalized_login = UPPER(LTRIM(RTRIM(login))),
    normalized_email = UPPER(LTRIM(RTRIM(email)));
GO

-- Contas nunca sao unidas automaticamente: em caso de colisao a migration falha e a resolucao e manual.
IF EXISTS (SELECT 1 FROM auth.users GROUP BY normalized_login HAVING COUNT(*) > 1)
    THROW 50001, 'Migration blocked: existing users have logins that collide after normalization. Resolve them manually and run again.', 1;
GO

IF EXISTS (SELECT 1 FROM auth.users WHERE deleted_at IS NULL GROUP BY normalized_email HAVING COUNT(*) > 1)
    THROW 50002, 'Migration blocked: existing users have emails that collide after normalization. Resolve them manually and run again.', 1;
GO

ALTER TABLE auth.users ALTER COLUMN normalized_login nvarchar(256) COLLATE Latin1_General_100_CS_AS NOT NULL;
ALTER TABLE auth.users ALTER COLUMN normalized_email nvarchar(256) COLLATE Latin1_General_100_CS_AS NOT NULL;
GO

DROP INDEX users_login_key ON auth.users;
DROP INDEX users_email_key ON auth.users;
GO

-- O login nunca e reaproveitado (spec 2026092306): vale pra todas as linhas, inclusive as excluidas.
CREATE UNIQUE INDEX users_normalized_login_key ON auth.users (normalized_login);
CREATE UNIQUE INDEX users_normalized_email_key ON auth.users (normalized_email) WHERE deleted_at IS NULL;
GO
