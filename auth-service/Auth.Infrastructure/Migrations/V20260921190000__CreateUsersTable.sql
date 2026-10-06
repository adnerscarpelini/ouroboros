-- login, email e token_hash usam colacao case-sensitive pra manter a semantica de comparacao exata.
-- O schema pode ja existir: docker/sqlserver/init/01-create-auth-db.sh o cria, dono auth_migrator, pra dar o GRANT ao auth_service.
IF SCHEMA_ID('auth') IS NULL
    EXEC('CREATE SCHEMA auth');
GO

CREATE TABLE auth.users (
    id bigint IDENTITY(1,1) NOT NULL,
    external_id uniqueidentifier NOT NULL,
    created_at datetimeoffset NOT NULL,
    updated_at datetimeoffset NULL,
    login nvarchar(256) COLLATE Latin1_General_100_CS_AS NOT NULL,
    full_name nvarchar(256) NOT NULL,
    email nvarchar(256) COLLATE Latin1_General_100_CS_AS NOT NULL,
    email_confirmed bit NOT NULL,
    password_hash nvarchar(512) NOT NULL,
    password_changed_at datetimeoffset NOT NULL,
    active bit NOT NULL,
    last_login_at datetimeoffset NULL,
    CONSTRAINT users_pkey PRIMARY KEY (id)
);
GO

CREATE UNIQUE INDEX users_external_id_key ON auth.users (external_id);
CREATE UNIQUE INDEX users_login_key ON auth.users (login);
CREATE UNIQUE INDEX users_email_key ON auth.users (email);
