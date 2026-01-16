using DigitalisationERP.Core.Abstractions;

namespace DigitalisationERP.Domain.Identity.Events;

/// <summary>
/// Domain event raised when a user is locked due to failed login attempts or admin action.
/// </summary>
public record UserLockedEvent(
    long UserId,
    string Reason,
    DateTime LockedAt,
    DateTime? LockedUntil) : IDomainEvent
{
    public DateTime OccurredAt => LockedAt;
    public long AggregateId => UserId;
}
