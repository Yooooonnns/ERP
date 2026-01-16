using MediatR;
using DigitalisationERP.Core;
using DigitalisationERP.Application.DTOs.Identity;

namespace DigitalisationERP.Application.Identity.Queries;

public class GetRoleHierarchyQuery : IRequest<Result<RoleHierarchyDto>>
{
    public long RoleId { get; set; }

    public GetRoleHierarchyQuery(long roleId)
    {
        RoleId = roleId;
    }
}
