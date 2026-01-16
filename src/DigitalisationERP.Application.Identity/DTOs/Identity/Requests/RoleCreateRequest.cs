namespace DigitalisationERP.Application.DTOs.Identity.Requests;

public class RoleCreateRequest
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public long? ParentRoleId { get; set; }
}
