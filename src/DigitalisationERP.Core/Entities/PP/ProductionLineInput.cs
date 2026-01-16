using DigitalisationERP.Core.Entities.MM;

namespace DigitalisationERP.Core.Entities.PP;

/// <summary>
/// Input raw material consumed by a production line.
/// QuantityPerUnit is the amount of raw material consumed per 1 unit of finished product.
/// </summary>
public class ProductionLineInput : BaseEntity
{
    public long ProductionLineDefinitionId { get; set; }

    public ProductionLineDefinition? ProductionLineDefinition { get; set; }

    public string MaterialNumber { get; set; } = string.Empty;

    public Material? Material { get; set; }

    public decimal QuantityPerUnit { get; set; }

    public string UnitOfMeasure { get; set; } = string.Empty;
}
