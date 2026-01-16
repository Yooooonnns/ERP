using System;

namespace DigitalisationERP.Core.Entities;

public class MaintenanceHistory
{
    public int Id { get; set; }
    public int MaintenanceScheduleId { get; set; }
    public MaintenanceSchedule MaintenanceSchedule { get; set; } = null!;
    
    public DateTime ExecutionDate { get; set; }
    public int DurationMinutes { get; set; }
    
    public string PerformedByUserId { get; set; } = string.Empty;
    public string PerformedByUserName { get; set; } = string.Empty;
    
    public string WorkPerformed { get; set; } = string.Empty;
    public string? PartsReplaced { get; set; } // JSON array
    public decimal Cost { get; set; }
    
    public string? IssuesFound { get; set; }
    public string? RecommendedActions { get; set; }
    
    public bool EquipmentFunctionalAfter { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
