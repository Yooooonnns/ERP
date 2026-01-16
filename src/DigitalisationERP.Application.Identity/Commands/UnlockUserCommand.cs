using DigitalisationERP.Core;
using MediatR;

namespace DigitalisationERP.Application.Identity.Commands;

public class UnlockUserCommand : IRequest<Result<bool>>
{
    public long UserId { get; set; }

    public UnlockUserCommand(long userId)
    {
        UserId = userId;
    }
}
