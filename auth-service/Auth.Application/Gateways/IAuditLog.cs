namespace Ouroboros.Auth.Application.Gateways;

using Ouroboros.Auth.Domain.Entities;

/// <summary>
/// Trilha de auditoria dos eventos de seguranca (so insercao). Evento de sucesso e registrado dentro da transacao da
/// operacao: se ela for desfeita, nao ha evento. Evento de falha e registrado fora de qualquer transacao (autocommit),
/// pra sobreviver a um rollback.
/// </summary>
public interface IAuditLog
{
    Task RecordAsync(AuditEvent auditEvent);
}

/// <summary>
/// Nunca leva senha, token, hash nem o login digitado numa tentativa contra conta inexistente: nesse caso
/// <see cref="UserExternalId"/> fica nulo. IP e user agent nao entram aqui: vem do <see cref="IRequestContext"/>.
/// </summary>
/// <param name="UserExternalId">Conta afetada (sem FK no banco, pro evento sobreviver a remocao de cadastro abandonado).</param>
/// <param name="ActorExternalId">Quem agiu, quando nao e o proprio usuario (um Admin).</param>
/// <param name="SessionId">Sessao, quando existir.</param>
/// <param name="Reason">Um dos codigos fixos de <see cref="AuditReason"/>.</param>
public sealed record AuditEvent(
    AuditEventType Type,
    AuditOutcome Outcome,
    Guid? UserExternalId = null,
    Guid? ActorExternalId = null,
    Guid? SessionId = null,
    string? Reason = null);
