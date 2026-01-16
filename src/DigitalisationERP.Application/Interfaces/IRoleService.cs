using DigitalisationERP.Application.DTOs.Role;
using DigitalisationERP.Core.Entities.Auth;

namespace DigitalisationERP.Application.Interfaces;

public interface IRoleService
{
    // Role CRUD operations
    Task<RoleDetailDto> CreateRoleAsync(CreateRoleRequest request, string currentUsername);
    Task<RoleDetailDto> GetRoleByIdAsync(long roleId);
    Task<RoleDetailDto?> GetRoleByNameAsync(string roleName);
    Task<List<RoleListDto>> GetAllRolesAsync(string? module = null, bool? isActive = null);
    Task<RoleDetailDto> UpdateRoleAsync(long roleId, UpdateRoleRequest request, string currentUsername);
    Task<bool> DeleteRoleAsync(long roleId);
    Task<bool> ActivateRoleAsync(long roleId, string currentUsername);
    Task<bool> DeactivateRoleAsync(long roleId, string currentUsername);

    // User-Role assignment
    Task<UserRoleDto> AssignRoleToUserAsync(AssignRoleToUserRequest request, string currentUsername);
    Task<bool> RemoveRoleFromUserAsync(RemoveRoleFromUserRequest request, string currentUsername);
    Task<List<UserRoleDto>> GetUserRolesAsync(long userId);
    Task<List<UserRoleDto>> GetRoleUsersAsync(long roleId);
    Task<bool> IsRoleAssignedToUserAsync(long userId, long roleId);

    // Authorization management
    Task<AuthorizationSummaryDto> AssignAuthorizationAsync(AssignAuthorizationRequest request, string currentUsername);
    Task<bool> RemoveAuthorizationFromRoleAsync(long roleId, long authorizationId);
    Task<List<AuthorizationSummaryDto>> GetRoleAuthorizationsAsync(long roleId);

    // Role validation
    Task<bool> RoleExistsAsync(long roleId);
    Task<bool> RoleNameExistsAsync(string roleName);
    Task<bool> CanDeleteRoleAsync(long roleId);
}
