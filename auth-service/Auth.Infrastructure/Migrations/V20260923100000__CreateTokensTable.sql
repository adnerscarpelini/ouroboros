CREATE TABLE auth.tokens (
    id bigint IDENTITY(1,1) NOT NULL,
    external_id uniqueidentifier NOT NULL,
    created_at datetimeoffset NOT NULL,
    updated_at datetimeoffset NULL,
    user_id bigint NOT NULL,
    type nvarchar(50) NOT NULL,
    token_hash nvarchar(128) COLLATE Latin1_General_100_CS_AS NOT NULL,
    expires_at datetimeoffset NOT NULL,
    used_at datetimeoffset NULL,
    CONSTRAINT tokens_pkey PRIMARY KEY (id),
    CONSTRAINT tokens_user_id_fkey FOREIGN KEY (user_id) REFERENCES auth.users (id)
);
GO

CREATE UNIQUE INDEX tokens_external_id_key ON auth.tokens (external_id);
CREATE UNIQUE INDEX tokens_token_hash_key ON auth.tokens (token_hash);
CREATE INDEX tokens_user_id_idx ON auth.tokens (user_id);
