using MediatR;
using DigitalisationERP.Core;
using DigitalisationERP.Application.DTOs.Identity;

namespace DigitalisationERP.Application.Identity.Queries;

public class GetRolePermissionsQuery : IRequest<Result<List<RoleAuthorizationDto>>>
{
    public long RoleId { get; set; }

    public GetRolePermissionsQuery(long roleId)
    {
        RoleId = roleId;
    }
}
