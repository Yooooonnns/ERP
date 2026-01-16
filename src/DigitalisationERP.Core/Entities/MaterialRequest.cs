using DigitalisationERP.Core.Entities.Auth;

namespace DigitalisationERP.Core.Entities;

/// <summary>
/// Represents a request for material delivery to a production post (bot notification)
/// </summary>
public class MaterialRequest : BaseEntity
{
    public string RequestNumber { get; set; } = string.Empty; // e.g., MR-2024-0001
    
    public long ProductionPostId { get; set; }
    public ProductionPost ProductionPost { get; set; } = null!;
    
    public long RequestedByUserId { get; set; }
    public User RequestedBy { get; set; } = null!;
    
    public string MaterialName { get; set; } = string.Empty;
    public decimal RequestedQuantity { get; set; }
    public string Unit { get; set; } = string.Empty; // kg, units, liters, etc.
    
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public RequestPriority Priority { get; set; } = RequestPriority.Normal;
    
    public DateTime RequestedAt { get; set; }
    public DateTime? FulfilledAt { get; set; }
    
    public long? FulfilledByUserId { get; set; }
    public User? FulfilledBy { get; set; }
    
    public string Notes { get; set; } = string.Empty;
    public bool BotNotified { get; set; } // Flag for automatic bot notification
    public DateTime? BotNotifiedAt { get; set; }
}

public enum RequestStatus
{
    Pending,
    Acknowledged,
    InTransit,
    Delivered,
    Cancelled
}

public enum RequestPriority
{
    Low,
    Normal,
    High,
    Emergency
}
