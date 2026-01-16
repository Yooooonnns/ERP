using DigitalisationERP.Application.DTOs.Identity;
using DigitalisationERP.Core;
using MediatR;

namespace DigitalisationERP.Application.Identity.Queries;

/// <summary>Query to get a single role by ID.</summary>
public class GetRoleQuery : IRequest<Result<RoleDto>>
{
    /// <summary>Gets the role ID.</summary>
    public long RoleId { get; }

    /// <summary>Initializes a new instance of the GetRoleQuery class.</summary>
    public GetRoleQuery(long roleId)
    {
        RoleId = roleId;
    }
}
