namespace DigitalisationERP.Application.DTOs.Identity;

public class RoleDto
{
    public long Id { get; set; }
    public string RoleCode { get; set; } = null!;
    public string RoleName { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string RoleType { get; set; } = null!;
    public bool IsActive { get; set; }
    public long? ParentRoleId { get; set; }
    public List<RoleDto> ChildRoles { get; set; } = new();
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
}
