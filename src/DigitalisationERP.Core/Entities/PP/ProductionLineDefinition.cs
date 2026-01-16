using DigitalisationERP.Core.Entities.MM;

namespace DigitalisationERP.Core.Entities.PP;

/// <summary>
/// Production Line definition that maps inputs (raw materials) to an output (finished product).
/// Used by the Stock Diagram and for posting OF consumption/production.
/// </summary>
public class ProductionLineDefinition : BaseEntity
{
    /// <summary>
    /// Logical line identifier (must match Desktop LineId).
    /// </summary>
    public string LineId { get; set; } = string.Empty;

    public string LineName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    /// <summary>
    /// Output finished product material number.
    /// </summary>
    public string OutputMaterialNumber { get; set; } = string.Empty;

    public Material? OutputMaterial { get; set; }

    public ICollection<ProductionLineInput> Inputs { get; set; } = new List<ProductionLineInput>();
}
