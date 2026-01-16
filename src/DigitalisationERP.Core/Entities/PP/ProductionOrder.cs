using DigitalisationERP.Core.Entities;

namespace DigitalisationERP.Core.Entities.PP;

/// <summary>
/// Production Order (SAP AFKO equivalent)
/// Manufacturing orders for production
/// </summary>
public class ProductionOrder : BaseEntity
{
    /// <summary>
    /// Production Order Number (AUFNR in SAP)
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Material to be produced
    /// </summary>
    public string MaterialNumber { get; set; } = string.Empty;

    /// <summary>
    /// Planned quantity to produce
    /// </summary>
    public decimal PlannedQuantity { get; set; }

    /// <summary>
    /// Actual quantity produced
    /// </summary>
    public decimal ActualQuantity { get; set; }

    /// <summary>
    /// Scrap quantity
    /// </summary>
    public decimal ScrapQuantity { get; set; }

    /// <summary>
    /// Production line/Work center
    /// </summary>
    public string WorkCenter { get; set; } = string.Empty;

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
    /// Production order status
    /// </summary>
    public ProductionOrderStatus Status { get; set; }

    /// <summary>
    /// Priority level
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Reference to customer order (if Make-to-Order)
    /// </summary>
    public string? SalesOrderNumber { get; set; }
    
    /// <summary>
    /// Kanban tasks associated with this production order
    /// </summary>
    public ICollection<ProductionTask> Tasks { get; set; } = new List<ProductionTask>();
}

public enum ProductionOrderStatus
{
    Created = 1,
    Released = 2,
    InProduction = 3,
    Completed = 4,
    TechnicallyCompleted = 5,
    Closed = 6,
    Cancelled = 7
}
