-- Spec 2026092515: a limpeza de tokens expirados filtra por expires_at.
CREATE INDEX tokens_expires_at_idx ON auth.tokens (expires_at);
CREATE INDEX refresh_tokens_expires_at_idx ON auth.refresh_tokens (expires_at);
