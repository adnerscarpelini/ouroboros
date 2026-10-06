namespace Ouroboros.Auth.Application.Fakes;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;

public sealed class FakeAuditLog : IAuditLog
{
    public List<AuditEvent> Events { get; } = new();

    public Exception? RecordException { get; set; }

    public Task RecordAsync(AuditEvent auditEvent)
    {
        if (RecordException is not null)
        {
            throw RecordException;
        }

        Events.Add(auditEvent);
        return Task.CompletedTask;
    }

    public AuditEvent Single(AuditEventType type) => Events.Single(auditEvent => auditEvent.Type == type);

    public bool Has(AuditEventType type) => Events.Any(auditEvent => auditEvent.Type == type);
}
