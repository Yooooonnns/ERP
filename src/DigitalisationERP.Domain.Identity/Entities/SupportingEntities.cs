namespace DigitalisationERP.Domain.Identity.Entities;

public class UserRoleAssignment
{
    public long UserRoleAssignmentId { get; set; }
    public long UserId { get; set; }
    public long RoleId { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public long? ModifiedBy { get; set; }

    public User? User { get; set; }
    public Role? Role { get; set; }
}

public class UserGroupAssignment
{
    public long UserGroupAssignmentId { get; set; }
    public long UserId { get; set; }
    public long UserGroupId { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public long? ModifiedBy { get; set; }

    public User? User { get; set; }
    public UserGroup? UserGroup { get; set; }
}

public class UserGroup
{
    public long UserGroupId { get; set; }
    public string UserGroupCode { get; set; } = null!;
    public string UserGroupName { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public long? ModifiedBy { get; set; }

    public ICollection<UserGroupAssignment> UserGroupAssignments { get; set; } = new List<UserGroupAssignment>();
}

public class AuthorizationObject
{
    public long AuthObjectId { get; set; }
    public string AuthObjectCode { get; set; } = null!;
    public string AuthObjectName { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public long? ModifiedBy { get; set; }

    public ICollection<AuthorizationField> AuthorizationFields { get; set; } = new List<AuthorizationField>();
    public ICollection<RoleAuthorization> RoleAuthorizations { get; set; } = new List<RoleAuthorization>();
}

public class AuthorizationField
{
    public long AuthFieldId { get; set; }
    public long AuthObjectId { get; set; }
    public string FieldName { get; set; } = null!;
    public string? FieldDescription { get; set; }
    public string? DataType { get; set; }
    public bool IsRequired { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public long? ModifiedBy { get; set; }

    public AuthorizationObject? AuthorizationObject { get; set; }
}

public class RoleAuthorization
{
    public long RoleAuthId { get; set; }
    public long RoleId { get; set; }
    public long AuthObjectId { get; set; }
    public string Activity { get; set; } = null!;
    public string? FieldValues { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public long? ModifiedBy { get; set; }

    public Role? Role { get; set; }
    public AuthorizationObject? AuthorizationObject { get; set; }
}

public class PasswordHistory
{
    public long PasswordHistoryId { get; set; }
    public long UserId { get; set; }
    public string PasswordHash { get; set; } = null!;
    public string PasswordSalt { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    public User? User { get; set; }
}

public class SessionLog
{
    public long SessionLogId { get; set; }
    public long UserId { get; set; }
    public string SessionToken { get; set; } = null!;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? DeviceInfo { get; set; }
    public DateTime LoginTime { get; set; }
    public DateTime? LogoutTime { get; set; }
    public double? SessionDuration { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastActivityTime { get; set; }
    public string? TerminationReason { get; set; }

    public User? User { get; set; }
}

public class AuditLog
{
    public long AuditLogId { get; set; }
    public long? UserId { get; set; }
    public string AuditAction { get; set; } = null!;
    public string TableName { get; set; } = null!;
    public string? RecordId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? ChangeDescription { get; set; }
    public string? IpAddress { get; set; }
    public string? SessionId { get; set; }
    public DateTime CreatedDate { get; set; }

    public User? User { get; set; }
    public SessionLog? SessionLog { get; set; }
}
