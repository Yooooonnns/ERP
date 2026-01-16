using System;
using System.Collections.Generic;

namespace DigitalisationERP.Core.Entities;

public class ShiftHandover
{
    public int Id { get; set; }
    public string HandoverNumber { get; set; } = string.Empty;
    
    // Shift information
    public DateTime ShiftDate { get; set; }
    public ShiftEnum Shift { get; set; }
    
    // Users
    public string OutgoingUserId { get; set; } = string.Empty;
    public string OutgoingUserName { get; set; } = string.Empty;
    
    public string? IncomingUserId { get; set; }
    public string? IncomingUserName { get; set; }
    
    // Production summary
    public int TargetQuantity { get; set; }
    public int ActualQuantity { get; set; }
    public int QualityRejects { get; set; }
    
    // Status updates
    public string ProductionStatus { get; set; } = string.Empty;
    public string? OutstandingIssues { get; set; }
    public string? MaterialStatus { get; set; }
    public string? EquipmentStatus { get; set; }
    
    // Handover notes
    public string GeneralNotes { get; set; } = string.Empty;
    public string? SafetyNotes { get; set; }
    public string? QualityNotes { get; set; }
    public string? MaintenanceNotes { get; set; }
    
    // Tasks to follow up
    public string? PendingTasks { get; set; } // JSON array
    
    // Related entities
    public string? ActiveProductionPostIds { get; set; } // JSON array
    public string? OpenIssueIds { get; set; } // JSON array
    
    // Acknowledgment
    public bool AcknowledgedByIncoming { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum ShiftEnum
{
    Morning,
    Afternoon,
    Night,
    DayShift,
    NightShift
}
