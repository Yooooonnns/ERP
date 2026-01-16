namespace DigitalisationERP.Core.Entities;

/// <summary>
/// Base entity following SAP table structure conventions
/// All entities inherit from this to ensure consistency
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Primary Key
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Client (MANDT in SAP) - Multi-tenant identifier
    /// </summary>
    public string ClientId { get; set; } = "001";

    /// <summary>
    /// Created by user
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Created on date
    /// </summary>
    public DateTime CreatedOn { get; set; }

    /// <summary>
    /// Last changed by user
    /// </summary>
    public string? ChangedBy { get; set; }

    /// <summary>
    /// Last changed on date
    /// </summary>
    public DateTime? ChangedOn { get; set; }
}
