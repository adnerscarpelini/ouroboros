ALTER TABLE auth.refresh_tokens
    ADD session_id uniqueidentifier NULL;
GO

-- Cada refresh token existente vira a propria sessao (NEWID() e avaliado por linha).
UPDATE auth.refresh_tokens
SET session_id = NEWID();
GO

ALTER TABLE auth.refresh_tokens
    ALTER COLUMN session_id uniqueidentifier NOT NULL;
GO

CREATE INDEX refresh_tokens_user_id_session_id_idx ON auth.refresh_tokens (user_id, session_id);
