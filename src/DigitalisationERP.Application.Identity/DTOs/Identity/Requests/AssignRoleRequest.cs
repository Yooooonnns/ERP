namespace DigitalisationERP.Application.DTOs.Identity.Requests;

public class AssignRoleRequest
{
    public List<long> RoleIds { get; set; } = new();
}
