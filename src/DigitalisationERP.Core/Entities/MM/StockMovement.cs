namespace DigitalisationERP.Core.Entities.MM;

/// <summary>
/// Stock/Inventory Movement (SAP MKPF/MSEG equivalent)
/// Tracks all material movements (goods receipt, goods issue, transfers)
/// </summary>
public class StockMovement : BaseEntity
{
    /// <summary>
    /// Material Document Number (MBLNR in SAP)
    /// </summary>
    public string DocumentNumber { get; set; } = string.Empty;

    /// <summary>
    /// Movement Type (101=GR, 261=GI, 311=Transfer, etc.)
    /// </summary>
    public string MovementType { get; set; } = string.Empty;

    /// <summary>
    /// Material Number
    /// </summary>
    public string MaterialNumber { get; set; } = string.Empty;

    /// <summary>
    /// Quantity moved
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Unit of Measure
    /// </summary>
    public string UnitOfMeasure { get; set; } = string.Empty;

    /// <summary>
    /// From Storage Location
    /// </summary>
    public string? FromStorageLocation { get; set; }

    /// <summary>
    /// To Storage Location
    /// </summary>
    public string? ToStorageLocation { get; set; }

    /// <summary>
    /// Reference to Production Order (if applicable)
    /// </summary>
    public string? ProductionOrderNumber { get; set; }

    /// <summary>
    /// Was this movement executed by robot?
    /// </summary>
    public bool RobotExecuted { get; set; }

    /// <summary>
    /// Robot ID that executed the movement
    /// </summary>
    public string? RobotId { get; set; }

    /// <summary>
    /// Posting Date
    /// </summary>
    public DateTime PostingDate { get; set; }

    /// <summary>
    /// Document Date
    /// </summary>
    public DateTime DocumentDate { get; set; }

    /// <summary>
    /// Movement status
    /// </summary>
    public MovementStatus Status { get; set; }
}

public enum MovementStatus
{
    Pending = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
    Failed = 5
}
