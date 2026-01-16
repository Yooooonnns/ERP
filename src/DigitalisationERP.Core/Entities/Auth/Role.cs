namespace DigitalisationERP.Core.Entities.Auth;

/// <summary>
/// Role entity - SAP Composite Role equivalent
/// Groups of authorizations assigned to users
/// </summary>
public class Role : BaseEntity
{
    /// <summary>
    /// Role name/code (e.g., SAP_ALL, Z_PRODUCTION_MANAGER)
    /// </summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// Display name/description
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of role purpose
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Role type/category
    /// </summary>
    public RoleType RoleType { get; set; }

    /// <summary>
    /// Is this role active?
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Is this a system role (cannot be deleted)?
    /// </summary>
    public bool IsSystemRole { get; set; }

    /// <summary>
    /// Module/Area this role belongs to
    /// </summary>
    public string? Module { get; set; }

    // Navigation properties
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RoleAuthorization> RoleAuthorizations { get; set; } = new List<RoleAuthorization>();
}

/// <summary>
/// SAP Role Types
/// </summary>
public enum RoleType
{
    /// <summary>
    /// Single role with specific authorizations
    /// </summary>
    Single = 1,

    /// <summary>
    /// Composite role combining multiple single roles
    /// </summary>
    Composite = 2,

    /// <summary>
    /// Standard SAP-delivered role
    /// </summary>
    Standard = 3,

    /// <summary>
    /// Custom role created by organization
    /// </summary>
    Custom = 4
}
