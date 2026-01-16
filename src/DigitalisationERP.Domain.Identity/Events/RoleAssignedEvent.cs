using DigitalisationERP.Core.Abstractions;

namespace DigitalisationERP.Domain.Identity.Events;

/// <summary>
/// Domain event raised when a role is assigned to a user.
/// </summary>
public record RoleAssignedEvent(
    long UserId,
    long RoleId,
    string RoleCode,
    DateTime AssignedAt) : IDomainEvent
{
    public DateTime OccurredAt => AssignedAt;
    public long AggregateId => UserId;
}
