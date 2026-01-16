using DigitalisationERP.Application.DTOs.Identity;
using DigitalisationERP.Core;
using MediatR;

namespace DigitalisationERP.Application.Identity.Queries;

/// <summary>Query to list all roles with pagination.</summary>
public class ListRolesQuery : IRequest<Result<PaginatedResult<RoleDto>>>
{
    /// <summary>Gets the page number (1-indexed).</summary>
    public int PageNumber { get; }

    /// <summary>Gets the page size.</summary>
    public int PageSize { get; }

    /// <summary>Initializes a new instance of the ListRolesQuery class.</summary>
    public ListRolesQuery(int pageNumber, int pageSize)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;
    }
}
