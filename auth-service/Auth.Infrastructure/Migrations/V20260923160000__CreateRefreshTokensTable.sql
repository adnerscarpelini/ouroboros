CREATE TABLE auth.refresh_tokens (
    id bigint IDENTITY(1,1) NOT NULL,
    external_id uniqueidentifier NOT NULL,
    created_at datetimeoffset NOT NULL,
    updated_at datetimeoffset NULL,
    user_id bigint NOT NULL,
    token_hash nvarchar(128) COLLATE Latin1_General_100_CS_AS NOT NULL,
    expires_at datetimeoffset NOT NULL,
    revoked_at datetimeoffset NULL,
    CONSTRAINT refresh_tokens_pkey PRIMARY KEY (id),
    CONSTRAINT refresh_tokens_user_id_fkey FOREIGN KEY (user_id) REFERENCES auth.users (id)
);
GO

CREATE UNIQUE INDEX refresh_tokens_external_id_key ON auth.refresh_tokens (external_id);
CREATE UNIQUE INDEX refresh_tokens_token_hash_key ON auth.refresh_tokens (token_hash);
CREATE INDEX refresh_tokens_user_id_idx ON auth.refresh_tokens (user_id);
