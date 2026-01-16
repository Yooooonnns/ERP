using System;
using System.Collections.Generic;

namespace DigitalisationERP.Core.Entities;

public class MaintenanceSchedule
{
    public int Id { get; set; }
    public long ProductionPostId { get; set; }
    public ProductionPost ProductionPost { get; set; } = null!;
    
    public string MaintenanceCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    public MaintenanceTypeEnum MaintenanceType { get; set; }
    public MaintenancePriorityEnum Priority { get; set; }
    
    // Scheduling
    public DateTime? ScheduledDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int? EstimatedDurationMinutes { get; set; }
    public int? ActualDurationMinutes { get; set; }
    
    // Predictive maintenance
    public int? TriggerUsageHours { get; set; }
    public int? TriggerCycleCount { get; set; }
    public DateTime? LastMaintenanceDate { get; set; }
    public int? CurrentUsageHours { get; set; }
    public int? CurrentCycleCount { get; set; }
    public double? HealthScore { get; set; } // 0-100
    
    // Recurrence
    public bool IsRecurring { get; set; }
    public int? RecurrenceIntervalDays { get; set; }
    
    // Status
    public MaintenanceStatusEnum Status { get; set; }
    
    // Assignment
    public string? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    
    // Parts and cost
    public string? RequiredParts { get; set; } // JSON array
    public decimal? EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    
    public string? CompletionNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;  // Alias for CreatedAt
    
    public ICollection<MaintenanceHistory> History { get; set; } = new List<MaintenanceHistory>();
}

public enum MaintenanceTypeEnum
{
    Preventive,
    Predictive,
    Corrective,
    Breakdown,
    Calibration,
    Inspection,
    Cleaning,
    Lubrication
}

public enum MaintenancePriorityEnum
{
    Low,
    Normal,
    High,
    Critical,
    Emergency
}

public enum MaintenanceStatusEnum
{
    Scheduled,
    Overdue,
    InProgress,
    Completed,
    Cancelled,
    Deferred
}
