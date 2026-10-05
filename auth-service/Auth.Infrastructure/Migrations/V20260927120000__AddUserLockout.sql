ALTER TABLE auth.users
    ADD access_failed_count int NOT NULL CONSTRAINT users_access_failed_count_default DEFAULT 0,
        lockout_end datetimeoffset NULL;
