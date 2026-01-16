using DigitalisationERP.Application.DTOs.Identity.Requests;
using DigitalisationERP.Core;
using MediatR;

namespace DigitalisationERP.Application.Identity.Commands;

/// <summary>Command to assign roles to a user.</summary>
public class AssignRoleCommand : IRequest<Result<string>>
{
    /// <summary>Gets the user ID.</summary>
    public long UserId { get; }

    /// <summary>Gets the role IDs to assign.</summary>
    public List<long> RoleIds { get; }

    /// <summary>Initializes a new instance of the AssignRoleCommand class.</summary>
    public AssignRoleCommand(long userId, List<long> roleIds)
    {
        UserId = userId;
        RoleIds = roleIds;
    }
}
