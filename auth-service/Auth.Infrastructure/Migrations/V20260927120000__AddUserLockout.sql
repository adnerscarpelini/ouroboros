ALTER TABLE auth.users
    ADD COLUMN access_failed_count integer NOT NULL DEFAULT 0,
    ADD COLUMN lockout_end timestamptz;
