namespace DigitalisationERP.Core.Abstractions;

/// <summary>
/// Marker interface for domain events.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Gets the date and time when the event occurred.
    /// </summary>
    DateTime OccurredAt { get; }
    
    /// <summary>
    /// Gets the ID of the aggregate that raised this event.
    /// </summary>
    long AggregateId { get; }
}
