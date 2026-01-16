namespace DigitalisationERP.Core.Entities;

/// <summary>
/// Represents a workstation/post in the production chain
/// </summary>
public class ProductionPost : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string PostName { get; set; } = string.Empty;                // Alias for Name
    public string Code { get; set; } = string.Empty; // e.g., POST-001
    public string PostCode { get; set; } = string.Empty;                // Alias for Code
    public string Description { get; set; } = string.Empty;
    public int SequenceOrder { get; set; } // Position in production chain
    public string Department { get; set; } = string.Empty;
    public PostStatus Status { get; set; } = PostStatus.Active;
    public int Capacity { get; set; } // Max concurrent tasks
    public int CurrentLoad { get; set; } // Current active tasks
    public int ProductionLineId { get; set; }                           // Foreign key to production line
    
    // Material thresholds
    public decimal MinRawMaterialLevel { get; set; }
    public decimal CurrentRawMaterialLevel { get; set; }
    
    // Navigation
    public ICollection<ProductionTask> Tasks { get; set; } = new List<ProductionTask>();
    public ICollection<MaterialRequest> MaterialRequests { get; set; } = new List<MaterialRequest>();
}

public enum PostStatus
{
    Active,
    Maintenance,
    Idle,
    Offline
}
