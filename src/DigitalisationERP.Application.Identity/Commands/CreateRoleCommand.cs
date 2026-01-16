using DigitalisationERP.Core;
using MediatR;

namespace DigitalisationERP.Application.Identity.Commands;

public class CreateRoleCommand : IRequest<Result<long>>
{
    public string RoleName { get; set; }
    public string Description { get; set; }
    public long? ParentRoleId { get; set; }

    public CreateRoleCommand(string roleName, string description, long? parentRoleId = null)
    {
        RoleName = roleName;
        Description = description;
        ParentRoleId = parentRoleId;
    }
}
