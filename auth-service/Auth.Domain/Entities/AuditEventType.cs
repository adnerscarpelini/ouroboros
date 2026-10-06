namespace Ouroboros.Auth.Domain.Entities;

/// <summary>
/// Eventos de seguranca registrados na trilha de auditoria (spec 2026092519). O nome do valor e o gravado em
/// <c>auth.audit_events.event_type</c>, e o CHECK da coluna lista os mesmos valores: um evento novo pede migration.
/// O refresh normal nao e auditado, pelo volume.
/// </summary>
public enum AuditEventType
{
    UserRegistered,
    EmailConfirmed,
    LoginSucceeded,
    LoginFailed,
    AccountLockedOut,
    RefreshTokenReuseDetected,
    Logout,
    LogoutAll,
    PasswordResetRequested,
    PasswordResetCompleted,
    PasswordChanged,
    UserDeleted,
}

public enum AuditOutcome
{
    Success,
    Failure,
}

/// <summary>
/// Codigos fixos do motivo de um evento. Nunca viram texto livre: o motivo nao pode carregar senha, token nem o que o
/// usuario digitou.
/// </summary>
public static class AuditReason
{
    public const string InvalidPassword = "invalid_password";
    public const string UnknownLogin = "unknown_login";
    public const string LockedOut = "locked_out";
    public const string InactiveAccount = "inactive_account";
    public const string TooManyFailedAttempts = "too_many_failed_attempts";
    public const string ReuseDetected = "reuse_detected";
}
