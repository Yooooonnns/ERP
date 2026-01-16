using MediatR;
using DigitalisationERP.Core;
using DigitalisationERP.Application.DTOs.Identity;

namespace DigitalisationERP.Application.Identity.Queries;

public class GetRoleUsersQuery : IRequest<Result<PaginatedResult<UserListDto>>>
{
    public long RoleId { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }

    public GetRoleUsersQuery(long roleId, int pageNumber = 1, int pageSize = 10)
    {
        RoleId = roleId;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }
}

