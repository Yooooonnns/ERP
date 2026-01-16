using DigitalisationERP.Core;
using MediatR;

namespace DigitalisationERP.Application.Identity.Commands;

public class DeleteUserCommand : IRequest<Result<bool>>
{
    public long UserId { get; set; }

    public DeleteUserCommand(long userId)
    {
        UserId = userId;
    }
}
