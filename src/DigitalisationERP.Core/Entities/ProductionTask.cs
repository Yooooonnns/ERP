using DigitalisationERP.Core.Entities.Auth;
using DigitalisationERP.Core.Entities.PP;

namespace DigitalisationERP.Core.Entities;

/// <summary>
/// Represents a Kanban task card in the production system
/// </summary>
public class ProductionTask : BaseEntity
{
    public string TaskNumber { get; set; } = string.Empty; // e.g., TASK-2024-0001
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskStatus Status { get; set; } = TaskStatus.Backlog;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    
    // Production details
    public long ProductionOrderId { get; set; }
    public PP.ProductionOrder ProductionOrder { get; set; } = null!;
    
    public long? AssignedPostId { get; set; }
    public ProductionPost? AssignedPost { get; set; }
    
    public long? AssignedUserId { get; set; }
    public User? AssignedUser { get; set; }
    
    // Tracking
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime DueDate { get; set; }
    public int EstimatedHours { get; set; }
    public int ActualHours { get; set; }
    
    // Materials
    public string RequiredMaterials { get; set; } = string.Empty; // JSON array
    public bool MaterialsAvailable { get; set; }
    
    // Quality
    public bool QualityCheckPassed { get; set; }
    public string? QualityNotes { get; set; }
}

public enum TaskStatus
{
    Backlog,
    ToDo,
    InProgress,
    QualityCheck,
    Done,
    Blocked
}

public enum TaskPriority
{
    Low,
    Medium,
    High,
    Urgent
}
