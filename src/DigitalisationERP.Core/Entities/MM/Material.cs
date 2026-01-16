using DigitalisationERP.Core.Enums;

namespace DigitalisationERP.Core.Entities.MM;

/// <summary>
/// Material Master (SAP MARA/MARC equivalent)
/// Raw materials, finished goods, spare parts
/// </summary>
public class Material : BaseEntity
{
    /// <summary>
    /// Material Number (MATNR in SAP)
    /// </summary>
    public string MaterialNumber { get; set; } = string.Empty;

    /// <summary>
    /// Material Description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Material Type (ROH=Raw Material, HALB=Semi-finished, FERT=Finished Product)
    /// </summary>
    public MaterialType MaterialType { get; set; }

    /// <summary>
    /// Base Unit of Measure (KG, L, PC, etc.)
    /// </summary>
    public string BaseUnitOfMeasure { get; set; } = string.Empty;

    /// <summary>
    /// Current stock quantity
    /// </summary>
    public decimal StockQuantity { get; set; }

    /// <summary>
    /// Minimum stock level (reorder point)
    /// </summary>
    public decimal MinimumStock { get; set; }

    /// <summary>
    /// Maximum stock level
    /// </summary>
    public decimal MaximumStock { get; set; }

    /// <summary>
    /// Standard price
    /// </summary>
    public decimal StandardPrice { get; set; }

    /// <summary>
    /// Is this material robot-compatible for automated feeding?
    /// </summary>
    public bool RobotCompatible { get; set; }

    /// <summary>
    /// Storage location code
    /// </summary>
    public string? StorageLocation { get; set; }

    /// <summary>
    /// Record status
    /// </summary>
    public RecordStatus Status { get; set; }
}

public enum MaterialType
{
    RawMaterial = 1,      // ROH - Raw materials
    SemiFinished = 2,     // HALB - Semi-finished products
    FinishedProduct = 3,  // FERT - Finished products
    TradingGoods = 4,     // HAWA - Trading goods
    SpareParts = 5        // ERSA - Spare parts
}
