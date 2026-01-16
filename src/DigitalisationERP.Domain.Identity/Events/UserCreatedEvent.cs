using DigitalisationERP.Core.Abstractions;

namespace DigitalisationERP.Domain.Identity.Events;

/// <summary>
/// Domain event raised when a new user is created.
/// </summary>
public record UserCreatedEvent(
    long UserId,
    string Username,
    string Email,
    string UserType,
    DateTime CreatedAt) : IDomainEvent
{
    public DateTime OccurredAt => CreatedAt;
    public long AggregateId => UserId;
}
