using System;
using System.Collections.Generic;

namespace DigitalisationERP.Core.Entities;

public class IssueEscalation
{
    public int Id { get; set; }
    public string IssueNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Context
    public long? ProductionPostId { get; set; }
    public ProductionPost? ProductionPost { get; set; }
    
    public long? ProductionTaskId { get; set; }
    public ProductionTask? ProductionTask { get; set; }
    
    // Classification
    public IssueTypeEnum IssueType { get; set; }
    public IssueSeverityEnum Severity { get; set; }
    public IssuePriorityEnum Priority { get; set; }
    
    // Assignment
    public string ReportedByUserId { get; set; } = string.Empty;
    public string ReportedByUserName { get; set; } = string.Empty;
    
    public string? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    
    // Status
    public IssueStatusEnum Status { get; set; }
    
    // Timestamps
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    
    // Resolution
    public string? ResolutionNotes { get; set; }
    public string? RootCause { get; set; }
    public string? CorrectiveAction { get; set; }
    
    // Impact
    public int? DowntimeMinutes { get; set; }
    public decimal? CostImpact { get; set; }
    
    // Escalation tracking
    public int EscalationLevel { get; set; } // 1, 2, 3...
    public DateTime? LastEscalatedAt { get; set; }
    
    public string? AttachmentUrls { get; set; } // JSON array - photos, documents
    
    public ICollection<ProductionChatMessage> Messages { get; set; } = new List<ProductionChatMessage>();
}

public enum IssueTypeEnum
{
    MachineBreakdown,
    QualityDefect,
    MaterialShortage,
    SafetyIncident,
    ProcessDeviation,
    ToolFailure,
    OperatorAbsence,
    Other
}

public enum IssueSeverityEnum
{
    Low,
    Medium,
    High,
    Critical
}

public enum IssuePriorityEnum
{
    Low,
    Normal,
    High,
    Urgent
}

public enum IssueStatusEnum
{
    Open,
    Acknowledged,
    InProgress,
    Escalated,
    Resolved,
    Closed,
    Cancelled
}
