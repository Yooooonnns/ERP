namespace DigitalisationERP.Application.DTOs.Identity.Requests;

public class RoleAuthorizationRequest
{
    public long RoleId { get; set; }
    public long AuthorizationObjectId { get; set; }
    public bool CanCreate { get; set; }
    public bool CanRead { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
}
