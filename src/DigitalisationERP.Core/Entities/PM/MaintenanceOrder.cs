namespace DigitalisationERP.Core.Entities.PM;

/// <summary>
/// Maintenance Order (SAP PM Work Order equivalent)
/// Preventive and corrective maintenance
/// </summary>
public class MaintenanceOrder : BaseEntity
{
    /// <summary>
    /// Maintenance Order Number
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Equipment Number
    /// </summary>
    public string EquipmentNumber { get; set; } = string.Empty;

    /// <summary>
    /// Maintenance Type
    /// </summary>
    public MaintenanceType MaintenanceType { get; set; }

    /// <summary>
    /// Description of work to be done
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Priority
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Planned start date
    /// </summary>
    public DateTime PlannedStartDate { get; set; }

    /// <summary>
    /// Planned end date
    /// </summary>
    public DateTime PlannedEndDate { get; set; }

    /// <summary>
    /// Actual start date
    /// </summary>
    public DateTime? ActualStartDate { get; set; }

    /// <summary>
    /// Actual end date
    /// </summary>
    public DateTime? ActualEndDate { get; set; }

    /// <summary>
    /// Assigned technician/planner
    /// </summary>
    public string? AssignedTo { get; set; }

    /// <summary>
    /// Estimated cost
    /// </summary>
    public decimal EstimatedCost { get; set; }

    /// <summary>
    /// Actual cost
    /// </summary>
    public decimal ActualCost { get; set; }

    /// <summary>
    /// Order status
    /// </summary>
    public MaintenanceOrderStatus Status { get; set; }

    /// <summary>
    /// Was this triggered by sensor alert?
    /// </summary>
    public bool TriggeredBySensor { get; set; }

    /// <summary>
    /// Reference to sensor reading that triggered this
    /// </summary>
    public long? TriggeringSensorReadingId { get; set; }

    /// <summary>
    /// Completion notes
    /// </summary>
    public string? CompletionNotes { get; set; }
}

public enum MaintenanceType
{
    Preventive = 1,
    Corrective = 2,
    Predictive = 3,
    Breakdown = 4,
    Inspection = 5
}

public enum MaintenanceOrderStatus
{
    Created = 1,
    Scheduled = 2,
    Released = 3,
    InProgress = 4,
    Completed = 5,
    Closed = 6,
    Cancelled = 7
}
