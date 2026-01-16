using DigitalisationERP.Application.DTOs.Identity;
using DigitalisationERP.Core;
using MediatR;

namespace DigitalisationERP.Application.Identity.Queries;

/// <summary>Query to list all users with pagination.</summary>
public class ListUsersQuery : IRequest<Result<PaginatedResult<UserListDto>>>
{
    /// <summary>Gets the page number (1-indexed).</summary>
    public int PageNumber { get; }

    /// <summary>Gets the page size.</summary>
    public int PageSize { get; }

    /// <summary>Initializes a new instance of the ListUsersQuery class.</summary>
    public ListUsersQuery(int pageNumber, int pageSize)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;
    }
}
