namespace DigitalisationERP.Domain.Identity.Entities;

/// <summary>
/// Role aggregate representing user roles and permissions with support for role hierarchy.
/// </summary>
public class Role
{
    /// <summary>Gets the unique role identifier.</summary>
    public long RoleId { get; private set; }

    /// <summary>Gets the role code (e.g., PROD_MNGR_001, DIR_001).</summary>
    public string RoleCode { get; private set; } = null!;

    /// <summary>Gets the role name.</summary>
    public string RoleName { get; private set; } = null!;

    /// <summary>Gets the role description.</summary>
    public string? Description { get; private set; }

    /// <summary>Gets the role type (SingleRole or CompositeRole).</summary>
    public string RoleType { get; private set; } = "SingleRole";

    /// <summary>Gets a value indicating whether the role is active.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Gets the parent role ID for role hierarchy.</summary>
    public long? ParentRoleId { get; private set; }

    /// <summary>Gets the parent role reference.</summary>
    public Role? ParentRole { get; private set; }

    /// <summary>Gets the child roles (for composite roles).</summary>
    public ICollection<Role> ChildRoles { get; private set; } = new List<Role>();

    /// <summary>Gets the creation date.</summary>
    public DateTime CreatedDate { get; private set; }

    /// <summary>Gets the modification date.</summary>
    public DateTime ModifiedDate { get; private set; }

    /// <summary>Gets the ID of the user who created this role.</summary>
    public long? CreatedBy { get; private set; }

    /// <summary>Gets the collection of role authorizations.</summary>
    public ICollection<RoleAuthorization> RoleAuthorizations { get; private set; } = new List<RoleAuthorization>();

    /// <summary>Gets the collection of user role assignments.</summary>
    public ICollection<UserRoleAssignment> UserRoleAssignments { get; private set; } = new List<UserRoleAssignment>();

    private Role() { }

    /// <summary>Creates a new single role.</summary>
    public static Role CreateSingle(string roleCode, string roleName, string? description = null, long? createdBy = null)
    {
        return new Role
        {
            RoleCode = roleCode,
            RoleName = roleName,
            Description = description,
            RoleType = "SingleRole",
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow,
            CreatedBy = createdBy
        };
    }

    /// <summary>Creates a new composite role that combines other roles.</summary>
    public static Role CreateComposite(string roleCode, string roleName, string? description = null, long? createdBy = null)
    {
        return new Role
        {
            RoleCode = roleCode,
            RoleName = roleName,
            Description = description,
            RoleType = "CompositeRole",
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow,
            CreatedBy = createdBy
        };
    }

    /// <summary>Creates a derived role (inherited from parent role).</summary>
    public static Role CreateDerived(string roleCode, string roleName, Role parentRole, string? description = null, long? createdBy = null)
    {
        return new Role
        {
            RoleCode = roleCode,
            RoleName = roleName,
            Description = description,
            RoleType = "SingleRole",
            IsActive = true,
            ParentRoleId = parentRole.RoleId,
            ParentRole = parentRole,
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow,
            CreatedBy = createdBy
        };
    }
}
