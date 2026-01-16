namespace DigitalisationERP.Core.Entities.Auth;

/// <summary>
/// Many-to-many relationship between Users and Roles
/// Similar to SAP AGR_USERS table
/// </summary>
public class UserRole : BaseEntity
{
    /// <summary>
    /// User ID
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Role ID
    /// </summary>
    public long RoleId { get; set; }

    /// <summary>
    /// Assignment valid from date
    /// </summary>
    public DateTime ValidFrom { get; set; }

    /// <summary>
    /// Assignment valid to date (null = no expiration)
    /// </summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>
    /// Who assigned this role
    /// </summary>
    public string AssignedBy { get; set; } = string.Empty;

    /// <summary>
    /// Assignment date
    /// </summary>
    public DateTime AssignedOn { get; set; }

    /// <summary>
    /// Is this assignment active?
    /// </summary>
    public bool IsActive { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
