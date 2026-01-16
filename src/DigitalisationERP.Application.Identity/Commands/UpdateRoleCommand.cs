using DigitalisationERP.Core;
using MediatR;

namespace DigitalisationERP.Application.Identity.Commands;

public class UpdateRoleCommand : IRequest<Result<bool>>
{
    public long RoleId { get; set; }
    public string RoleName { get; set; }
    public string Description { get; set; }
    public long? ParentRoleId { get; set; }

    public UpdateRoleCommand(long roleId, string roleName, string description, long? parentRoleId = null)
    {
        RoleId = roleId;
        RoleName = roleName;
        Description = description;
        ParentRoleId = parentRoleId;
    }
}
