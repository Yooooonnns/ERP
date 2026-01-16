using DigitalisationERP.Core.Abstractions;

namespace DigitalisationERP.Domain.Identity.Events;

/// <summary>
/// Domain event raised when a user's password is changed.
/// </summary>
public record PasswordChangedEvent(
    long UserId,
    DateTime ChangedAt) : IDomainEvent
{
    public DateTime OccurredAt => ChangedAt;
    public long AggregateId => UserId;
}
