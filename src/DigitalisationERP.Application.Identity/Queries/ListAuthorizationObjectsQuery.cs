using MediatR;
using DigitalisationERP.Core;
using DigitalisationERP.Application.DTOs.Identity;

namespace DigitalisationERP.Application.Identity.Queries;

public class ListAuthorizationObjectsQuery : IRequest<Result<List<AuthorizationObjectDto>>>
{
}
