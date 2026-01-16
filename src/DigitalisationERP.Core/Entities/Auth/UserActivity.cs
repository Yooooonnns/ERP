namespace DigitalisationERP.Core.Entities.Auth;

/// <summary>
/// Audit log for user activities
/// Similar to SAP Change Documents and Security Audit Log
/// </summary>
public class UserActivity : BaseEntity
{
    /// <summary>
    /// User ID
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Username for quick reference
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Activity type
    /// </summary>
    public ActivityType ActivityType { get; set; }

    /// <summary>
    /// Activity description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Module/Area (MM, PP, FI, etc.)
    /// </summary>
    public string? Module { get; set; }

    /// <summary>
    /// Entity type affected (Material, ProductionOrder, etc.)
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Entity ID affected
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// IP address
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent / device info
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Activity timestamp
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Was the activity successful?
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Additional data (JSON)
    /// </summary>
    public string? AdditionalData { get; set; }
}

/// <summary>
/// Types of user activities to log
/// </summary>
public enum ActivityType
{
    Login = 1,
    Logout = 2,
    FailedLogin = 3,
    PasswordChange = 4,
    PasswordReset = 5,
    PasswordResetRequested = 6,
    EmailVerified = 7,
    Create = 10,
    Read = 11,
    Update = 12,
    Delete = 13,
    Export = 20,
    Import = 21,
    Print = 22,
    Download = 23,
    RoleAssignment = 30,
    AuthorizationChange = 31,
    UserCreation = 32,
    UserCreated = 33,
    UserDeactivation = 34,
    ApiAccess = 40,
    RobotCommand = 41,
    SensorDataAccess = 42,
    Other = 99
}
