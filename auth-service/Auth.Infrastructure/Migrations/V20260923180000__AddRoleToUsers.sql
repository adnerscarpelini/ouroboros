ALTER TABLE auth.users
    ADD role nvarchar(20) NOT NULL CONSTRAINT users_role_default DEFAULT 'User';
GO

ALTER TABLE auth.users
    ADD CONSTRAINT users_role_check CHECK (role IN ('User', 'Admin'));
