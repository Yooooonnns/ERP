namespace DigitalisationERP.Domain.Identity.Enums;

/// <summary>
/// Role types for managing role inheritance and structure.
/// </summary>
public enum RoleTypeEnum
{
    /// <summary>Single role - directly assigned to users</summary>
    SingleRole = 1,

    /// <summary>Composite role - combines multiple single roles</summary>
    CompositeRole = 2
}
