using DigitalisationERP.Core;
using MediatR;

namespace DigitalisationERP.Application.Identity.Commands;

public class AssignAuthorizationCommand : IRequest<Result<long>>
{
    public long RoleId { get; set; }
    public long AuthorizationObjectId { get; set; }
    public bool CanCreate { get; set; }
    public bool CanRead { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }

    public AssignAuthorizationCommand(long roleId, long authorizationObjectId, 
        bool canCreate, bool canRead, bool canUpdate, bool canDelete)
    {
        RoleId = roleId;
        AuthorizationObjectId = authorizationObjectId;
        CanCreate = canCreate;
        CanRead = canRead;
        CanUpdate = canUpdate;
        CanDelete = canDelete;
    }
}
