using DigitalisationERP.Core;
using MediatR;

namespace DigitalisationERP.Application.Identity.Commands;

public class LogoutCommand : IRequest<Result<bool>>
{
    public long UserId { get; set; }

    public LogoutCommand(long userId)
    {
        UserId = userId;
    }
}
