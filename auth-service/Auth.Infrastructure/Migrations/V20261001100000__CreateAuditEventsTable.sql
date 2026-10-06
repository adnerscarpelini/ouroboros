-- Spec 2026092519: trilha de auditoria dos eventos de seguranca, so de insercao.
-- user_external_id nao tem FK de proposito: o evento sobrevive a remocao de cadastro abandonado.
CREATE TABLE auth.audit_events (
    id bigint IDENTITY(1,1) NOT NULL,
    external_id uniqueidentifier NOT NULL,
    occurred_at datetimeoffset NOT NULL,
    event_type nvarchar(50) NOT NULL,
    outcome nvarchar(10) NOT NULL,
    user_external_id uniqueidentifier NULL,
    actor_external_id uniqueidentifier NULL,
    session_id uniqueidentifier NULL,
    ip_address nvarchar(45) NULL,
    user_agent nvarchar(256) NULL,
    reason nvarchar(50) NULL,
    CONSTRAINT audit_events_pkey PRIMARY KEY (id),
    CONSTRAINT audit_events_event_type_check CHECK (event_type IN (
        N'UserRegistered',
        N'EmailConfirmed',
        N'LoginSucceeded',
        N'LoginFailed',
        N'AccountLockedOut',
        N'RefreshTokenReuseDetected',
        N'Logout',
        N'LogoutAll',
        N'PasswordResetRequested',
        N'PasswordResetCompleted',
        N'PasswordChanged',
        N'UserDeleted')),
    CONSTRAINT audit_events_outcome_check CHECK (outcome IN (N'Success', N'Failure'))
);
GO

CREATE UNIQUE INDEX audit_events_external_id_key ON auth.audit_events (external_id);
CREATE INDEX audit_events_user_external_id_occurred_at_idx ON auth.audit_events (user_external_id, occurred_at);
CREATE INDEX audit_events_occurred_at_idx ON auth.audit_events (occurred_at);
GO

-- So INSERT e SELECT: o papel de DML da aplicacao (o que recebeu o GRANT no schema, ver docker/sqlserver/init) perde
-- UPDATE e DELETE nesta tabela. O DENY no objeto vence o GRANT no schema e vale pra qualquer nome de papel. Quem e
-- db_owner (o auth_migrator) nao e afetado por DENY, e e ele quem roda a retencao.
DECLARE @principal sysname;
DECLARE @sql nvarchar(max);

DECLARE grantees CURSOR LOCAL FAST_FORWARD FOR
    SELECT DISTINCT USER_NAME(permissions.grantee_principal_id)
    FROM sys.database_permissions AS permissions
    WHERE permissions.class = 3
        AND permissions.major_id = SCHEMA_ID('auth')
        AND permissions.permission_name IN ('UPDATE', 'DELETE')
        AND permissions.state IN ('G', 'W');

OPEN grantees;
FETCH NEXT FROM grantees INTO @principal;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'DENY UPDATE, DELETE ON auth.audit_events TO ' + QUOTENAME(@principal);
    EXEC(@sql);
    FETCH NEXT FROM grantees INTO @principal;
END;

CLOSE grantees;
DEALLOCATE grantees;
GO
