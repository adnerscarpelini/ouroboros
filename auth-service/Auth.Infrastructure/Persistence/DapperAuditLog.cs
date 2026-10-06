namespace Ouroboros.Auth.Infrastructure.Persistence;

using Ouroboros.Auth.Application.Gateways;

// Hints de leitura (skill ouroboros-dba): so INSERT aqui, sem hint. O papel auth_service tem INSERT e SELECT na tabela,
// nunca UPDATE nem DELETE (migration V20261001100000).
public sealed class DapperAuditLog : IAuditLog
{
    private const int MaxIpAddressLength = 45;
    private const int MaxUserAgentLength = 256;

    private readonly DbSession _session;
    private readonly IRequestContext _requestContext;

    public DapperAuditLog(
        DbSession session,
        IRequestContext requestContext)
    {
        _session = session;
        _requestContext = requestContext;
    }

    // Dentro de uma IUnitOfWork o INSERT usa a mesma transacao da operacao. Fora dela e autocommit.
    public async Task RecordAsync(AuditEvent auditEvent)
    {
        const string sql = """
            INSERT INTO auth.audit_events (
                external_id,
                occurred_at,
                event_type,
                outcome,
                user_external_id,
                actor_external_id,
                session_id,
                ip_address,
                user_agent,
                reason
            )
            VALUES (
                @ExternalId,
                @OccurredAt,
                @EventType,
                @Outcome,
                @UserExternalId,
                @ActorExternalId,
                @SessionId,
                @IpAddress,
                @UserAgent,
                @Reason
            );
            """;

        await _session.ExecuteAsync(
            sql,
            new
            {
                ExternalId = Guid.NewGuid(),
                OccurredAt = DateTimeOffset.UtcNow,
                EventType = auditEvent.Type.ToString(),
                Outcome = auditEvent.Outcome.ToString(),
                auditEvent.UserExternalId,
                auditEvent.ActorExternalId,
                auditEvent.SessionId,
                IpAddress = Truncate(_requestContext.IpAddress, MaxIpAddressLength),
                UserAgent = Truncate(_requestContext.UserAgent, MaxUserAgentLength),
                auditEvent.Reason,
            });
    }

    private static string? Truncate(
        string? value,
        int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
