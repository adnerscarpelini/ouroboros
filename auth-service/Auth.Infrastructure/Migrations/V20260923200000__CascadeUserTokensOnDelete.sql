-- Remover um usuario (cadastro abandonado) leva junto os tokens e refresh tokens dele.
ALTER TABLE auth.tokens
    DROP CONSTRAINT tokens_user_id_fkey,
    ADD CONSTRAINT tokens_user_id_fkey FOREIGN KEY (user_id) REFERENCES auth.users (id) ON DELETE CASCADE;

ALTER TABLE auth.refresh_tokens
    DROP CONSTRAINT refresh_tokens_user_id_fkey,
    ADD CONSTRAINT refresh_tokens_user_id_fkey FOREIGN KEY (user_id) REFERENCES auth.users (id) ON DELETE CASCADE;
