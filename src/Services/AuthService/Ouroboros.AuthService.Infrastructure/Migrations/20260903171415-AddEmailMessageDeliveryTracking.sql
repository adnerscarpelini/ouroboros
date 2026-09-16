ALTER TABLE common.email_messages
    ADD COLUMN attempt_count integer NOT NULL DEFAULT 0,
    ADD COLUMN last_attempt_at timestamp with time zone NULL,
    ADD COLUMN last_error text NULL;
