using DigitalisationERP.Core;
using MediatR;

namespace DigitalisationERP.Application.Identity.Commands;

public class LockUserCommand : IRequest<Result<bool>>
{
    public long UserId { get; set; }
    public int LockDurationMinutes { get; set; }

    public LockUserCommand(long userId, int lockDurationMinutes = 30)
    {
        UserId = userId;
        LockDurationMinutes = lockDurationMinutes;
    }
}
