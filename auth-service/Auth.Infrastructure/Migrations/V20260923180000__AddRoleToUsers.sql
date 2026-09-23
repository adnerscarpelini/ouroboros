ALTER TABLE auth.users
    ADD COLUMN role text NOT NULL DEFAULT 'User',
    ADD CONSTRAINT users_role_check CHECK (role IN ('User', 'Admin'));
