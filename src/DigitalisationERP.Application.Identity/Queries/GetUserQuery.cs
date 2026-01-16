using DigitalisationERP.Application.DTOs.Identity;
using DigitalisationERP.Core;
using MediatR;

namespace DigitalisationERP.Application.Identity.Queries;

/// <summary>Query to get a single user by ID.</summary>
public class GetUserQuery : IRequest<Result<UserDto>>
{
    /// <summary>Gets the user ID.</summary>
    public long UserId { get; }

    /// <summary>Initializes a new instance of the GetUserQuery class.</summary>
    public GetUserQuery(long userId)
    {
        UserId = userId;
    }
}
