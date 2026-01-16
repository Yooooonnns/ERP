using DigitalisationERP.Core.Abstractions;

namespace DigitalisationERP.Domain.Identity.Events;

/// <summary>
/// Domain event raised when an unauthorized access attempt is detected.
/// </summary>
public record UnauthorizedAccessAttemptedEvent(
    long? UserId,
    string Action,
    string? IpAddress,
    DateTime AttemptedAt) : IDomainEvent
{
    public DateTime OccurredAt => AttemptedAt;
    public long AggregateId => UserId ?? 0;
}
