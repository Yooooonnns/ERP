namespace DigitalisationERP.Core.Entities.Auth;

/// <summary>
/// Authorization Object - SAP Authorization Object equivalent
/// Defines what actions can be performed on what objects
/// Similar to SAP tables TOBJ, USR10, USR11, USR12
/// </summary>
public class Authorization : BaseEntity
{
    /// <summary>
    /// Authorization object code (e.g., M_MATE_WRK, F_BKPF_BUK)
    /// </summary>
    public string ObjectCode { get; set; } = string.Empty;

    /// <summary>
    /// Display name
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Module (MM, PP, FI, etc.)
    /// </summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>
    /// Object class/category
    /// </summary>
    public string ObjectClass { get; set; } = string.Empty;

    /// <summary>
    /// Is this a standard authorization object?
    /// </summary>
    public bool IsStandard { get; set; }

    /// <summary>
    /// Is this authorization active?
    /// </summary>
    public bool IsActive { get; set; }

    // Navigation properties
    public ICollection<AuthorizationField> Fields { get; set; } = new List<AuthorizationField>();
    public ICollection<RoleAuthorization> RoleAuthorizations { get; set; } = new List<RoleAuthorization>();
}

/// <summary>
/// Authorization Fields - Individual fields within authorization object
/// (Activity, Plant, Material Type, etc.)
/// </summary>
public class AuthorizationField : BaseEntity
{
    /// <summary>
    /// Authorization ID
    /// </summary>
    public long AuthorizationId { get; set; }

    /// <summary>
    /// Field name (ACTVT=Activity, WERKS=Plant, MTART=Material Type)
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Display name
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Field data type
    /// </summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// Is this field mandatory?
    /// </summary>
    public bool IsMandatory { get; set; }

    /// <summary>
    /// Field description
    /// </summary>
    public string? Description { get; set; }

    // Navigation property
    public Authorization Authorization { get; set; } = null!;
}

/// <summary>
/// Role Authorization - Links roles to authorization objects with field values
/// Similar to SAP AGR_1251 table
/// </summary>
public class RoleAuthorization : BaseEntity
{
    /// <summary>
    /// Role ID
    /// </summary>
    public long RoleId { get; set; }

    /// <summary>
    /// Authorization Object ID
    /// </summary>
    public long AuthorizationId { get; set; }

    /// <summary>
    /// Field name
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Field value (can use wildcards like *, ranges, etc.)
    /// </summary>
    public string FieldValue { get; set; } = string.Empty;

    /// <summary>
    /// From value (for ranges)
    /// </summary>
    public string? FromValue { get; set; }

    /// <summary>
    /// To value (for ranges)
    /// </summary>
    public string? ToValue { get; set; }

    /// <summary>
    /// Is this authorization active?
    /// </summary>
    public bool IsActive { get; set; }

    // Navigation properties
    public Role Role { get; set; } = null!;
    public Authorization Authorization { get; set; } = null!;
}
