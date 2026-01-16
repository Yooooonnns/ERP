namespace DigitalisationERP.Core.Entities.Auth;

/// <summary>
/// User entity following SAP user management principles
/// Similar to SAP USR02 table
/// </summary>
public class User : BaseEntity
{
    /// <summary>
    /// Username (BNAME in SAP) - Unique identifier
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Email verified flag
    /// </summary>
    public bool EmailVerified { get; set; }

    /// <summary>
    /// Email verified timestamp
    /// </summary>
    public DateTime? EmailVerifiedAt { get; set; }

    /// <summary>
    /// Password hash (never store plain text)
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// First name
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Last name
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// User type (Dialog, Service, System, etc.)
    /// </summary>
    public UserType UserType { get; set; }

    /// <summary>
    /// Account status
    /// </summary>
    public UserStatus Status { get; set; }

    /// <summary>
    /// Account valid from date
    /// </summary>
    public DateTime ValidFrom { get; set; }

    /// <summary>
    /// Account valid to date (null = no expiration)
    /// </summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>
    /// Last login timestamp
    /// </summary>
    public DateTime? LastLogin { get; set; }

    /// <summary>
    /// Failed login attempts counter
    /// </summary>
    public int FailedLoginAttempts { get; set; }

    /// <summary>
    /// Account locked until (after too many failed attempts)
    /// </summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>
    /// Password last changed date
    /// </summary>
    public DateTime? PasswordLastChanged { get; set; }

    /// <summary>
    /// Force password change on next login
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>
    /// Language/Locale (EN, FR, DE, etc.)
    /// </summary>
    public string Language { get; set; } = "EN";

    /// <summary>
    /// Time zone
    /// </summary>
    public string TimeZone { get; set; } = "UTC";

    /// <summary>
    /// Employee number (if linked to HR)
    /// </summary>
    public string? EmployeeNumber { get; set; }

    /// <summary>
    /// Department
    /// </summary>
    public string? Department { get; set; }

    /// <summary>
    /// Phone number
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Refresh token for JWT authentication
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Refresh token expiry
    /// </summary>
    public DateTime? RefreshTokenExpiry { get; set; }

    // Navigation properties
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

/// <summary>
/// SAP User Types
/// </summary>
public enum UserType
{
    /// <summary>
    /// Dialog/Interactive User - Regular employee with UI access
    /// </summary>
    Dialog = 1,

    /// <summary>
    /// Service User - For external systems/APIs (like S-User)
    /// </summary>
    Service = 2,

    /// <summary>
    /// System User - For internal system communications
    /// </summary>
    System = 3,

    /// <summary>
    /// Communication User - For external B2B communication
    /// </summary>
    Communication = 4,

    /// <summary>
    /// Reference User - Template for authorization assignment
    /// </summary>
    Reference = 5
}

/// <summary>
/// User account status
/// </summary>
public enum UserStatus
{
    Active = 1,
    Inactive = 2,
    Locked = 3,
    Expired = 4,
    PendingActivation = 5
}
