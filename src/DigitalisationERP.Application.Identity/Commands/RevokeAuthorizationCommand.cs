using DigitalisationERP.Core;
using MediatR;

namespace DigitalisationERP.Application.Identity.Commands;

public class RevokeAuthorizationCommand : IRequest<Result<bool>>
{
    public long RoleAuthorizationId { get; set; }

    public RevokeAuthorizationCommand(long roleAuthorizationId)
    {
        RoleAuthorizationId = roleAuthorizationId;
    }
}
