namespace DigitalisationERP.Application.DTOs.Role;

/// <summary>
/// DTO for creating a new role
/// </summary>
public class CreateRoleRequest
{
    public string RoleName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RoleType { get; set; } = "Custom"; // Single, Composite, Standard, Custom
    public string Module { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO for updating an existing role
/// </summary>
public class UpdateRoleRequest
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Module { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>
/// DTO for assigning a role to a user
/// </summary>
public class AssignRoleToUserRequest
{
    public long UserId { get; set; }
    public long RoleId { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
}

/// <summary>
/// DTO for removing a role from a user
/// </summary>
public class RemoveRoleFromUserRequest
{
    public long UserId { get; set; }
    public long RoleId { get; set; }
}

/// <summary>
/// DTO for assigning authorization to a role
/// </summary>
public class AssignAuthorizationRequest
{
    public long RoleId { get; set; }
    public long AuthorizationId { get; set; }
    public List<AuthorizationFieldValueDto> FieldValues { get; set; } = new();
}

/// <summary>
/// DTO for authorization field values
/// </summary>
public class AuthorizationFieldValueDto
{
    public string FieldName { get; set; } = string.Empty;
    public string FieldValue { get; set; } = string.Empty;
    public string? FromValue { get; set; }
    public string? ToValue { get; set; }
}

/// <summary>
/// DTO for role details response
/// </summary>
public class RoleDetailDto
{
    public long Id { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RoleType { get; set; } = string.Empty;
    public string? Module { get; set; }
    public bool IsActive { get; set; }
    public int UserCount { get; set; }
    public List<AuthorizationSummaryDto> Authorizations { get; set; } = new();
    public DateTime CreatedOn { get; set; }
    public string? CreatedBy { get; set; }
}

/// <summary>
/// DTO for authorization summary
/// </summary>
public class AuthorizationSummaryDto
{
    public long Id { get; set; }
    public string ObjectCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public List<AuthorizationFieldValueDto> FieldValues { get; set; } = new();
}

/// <summary>
/// DTO for user role assignment
/// </summary>
public class UserRoleDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public long RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string RoleDisplayName { get; set; } = string.Empty;
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public DateTime AssignedOn { get; set; }
    public string? AssignedBy { get; set; }
}

/// <summary>
/// DTO for role list item
/// </summary>
public class RoleListDto
{
    public long Id { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RoleType { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int UserCount { get; set; }
    public int AuthorizationCount { get; set; }
}
