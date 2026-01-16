using MediatR;
using DigitalisationERP.Core;
using DigitalisationERP.Application.DTOs.Identity;

namespace DigitalisationERP.Application.Identity.Queries;

public class GetAuthorizationObjectQuery : IRequest<Result<AuthorizationObjectDto>>
{
    public long ObjectId { get; set; }

    public GetAuthorizationObjectQuery(long objectId)
    {
        ObjectId = objectId;
    }
}
