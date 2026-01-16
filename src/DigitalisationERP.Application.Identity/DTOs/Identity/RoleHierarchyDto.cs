namespace DigitalisationERP.Application.DTOs.Identity;

public class RoleHierarchyDto
{
    public long RoleId { get; set; }
    public string RoleCode { get; set; } = null!;
    public string RoleName { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string RoleType { get; set; } = null!;
    public RoleDto ParentRole { get; set; } = null!;
    public List<RoleDto> ChildRoles { get; set; } = new();
    public DateTime CreatedDate { get; set; }
}
