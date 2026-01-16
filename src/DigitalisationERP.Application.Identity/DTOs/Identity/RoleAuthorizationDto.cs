namespace DigitalisationERP.Application.DTOs.Identity;

public class RoleAuthorizationDto
{
    public long RoleAuthorizationId { get; set; }
    public long RoleId { get; set; }
    public string RoleCode { get; set; } = null!;
    public long AuthorizationObjectId { get; set; }
    public string AuthObjectName { get; set; } = null!;
    public bool CanCreate { get; set; }
    public bool CanRead { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
    public DateTime CreatedDate { get; set; }
}
