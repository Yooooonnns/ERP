using System;
using System.Collections.Generic;

namespace DigitalisationERP.Core.Entities;

public class ProductionSchedule
{
    public int Id { get; set; }
    public string ScheduleNumber { get; set; } = string.Empty;
    
    // Links to production order
    public long ProductionOrderId { get; set; }
    
    // Scheduling
    public DateTime PlannedStartDate { get; set; }
    public DateTime PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    
    public int PlannedDurationMinutes { get; set; }
    public int? ActualDurationMinutes { get; set; }
    
    // Priority and sequencing
    public int Priority { get; set; }
    public int SequenceNumber { get; set; }
    
    // Resource allocation
    public long? AssignedProductionPostId { get; set; }
    public ProductionPost? AssignedProductionPost { get; set; }
    
    public string? AssignedOperatorIds { get; set; } // JSON array of user IDs
    
    // Setup time
    public int? SetupTimeMinutes { get; set; }
    public string? SetupRequirements { get; set; }
    
    // Status
    public ScheduleStatusEnum Status { get; set; }
    
    // Constraints
    public string? MaterialConstraints { get; set; } // JSON
    public string? ToolConstraints { get; set; } // JSON
    public string? SkillConstraints { get; set; } // JSON
    
    // Dependencies
    public int? PredecessorScheduleId { get; set; }
    public ProductionSchedule? PredecessorSchedule { get; set; }
    
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    public ICollection<ProductionSchedule> DependentSchedules { get; set; } = new List<ProductionSchedule>();
}

public enum ScheduleStatusEnum
{
    Planned,
    Ready,
    InProgress,
    OnHold,
    Completed,
    Cancelled,
    Delayed
}
