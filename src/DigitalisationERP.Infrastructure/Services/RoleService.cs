using DigitalisationERP.Application.DTOs.Role;
using DigitalisationERP.Application.Interfaces;
using DigitalisationERP.Core.Entities.Auth;
using DigitalisationERP.Core.Enums;
using DigitalisationERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DigitalisationERP.Infrastructure.Services;

public class RoleService : IRoleService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RoleService> _logger;

    public RoleService(ApplicationDbContext context, ILogger<RoleService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<RoleDetailDto> CreateRoleAsync(CreateRoleRequest request, string currentUsername)
    {
        _logger.LogInformation("Creating role {RoleName} by {User}", request.RoleName, currentUsername);

        // Validate role name doesn't exist
        if (await _context.Roles.AnyAsync(r => r.RoleName == request.RoleName))
        {
            throw new InvalidOperationException($"Role with name '{request.RoleName}' already exists");
        }

        // Parse role type
        if (!Enum.TryParse<RoleType>(request.RoleType, out var roleType))
        {
            throw new ArgumentException($"Invalid role type: {request.RoleType}");
        }

        var role = new Role
        {
            RoleName = request.RoleName.ToUpper(),
            DisplayName = request.DisplayName,
            Description = request.Description,
            RoleType = roleType,
            Module = request.Module.ToUpper(),
            IsActive = request.IsActive,
            ClientId = "001" // TODO: Get from context
        };

        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Role {RoleName} created successfully with ID {RoleId}", role.RoleName, role.Id);

        return await GetRoleByIdAsync(role.Id);
    }

    public async Task<RoleDetailDto> GetRoleByIdAsync(long roleId)
    {
        var role = await _context.Roles
            .Include(r => r.RoleAuthorizations)
                .ThenInclude(ra => ra.Authorization)
            .Include(r => r.UserRoles)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role == null)
        {
            throw new KeyNotFoundException($"Role with ID {roleId} not found");
        }

        return new RoleDetailDto
        {
            Id = role.Id,
            RoleName = role.RoleName,
            DisplayName = role.DisplayName,
            Description = role.Description,
            RoleType = role.RoleType.ToString(),
            Module = role.Module,
            IsActive = role.IsActive,
            UserCount = role.UserRoles.Count,
            Authorizations = role.RoleAuthorizations
                .GroupBy(ra => ra.Authorization)
                .Select(g => new AuthorizationSummaryDto
                {
                    Id = g.Key.Id,
                    ObjectCode = g.Key.ObjectCode,
                    DisplayName = g.Key.DisplayName,
                    Module = g.Key.Module,
                    FieldValues = g.Select(ra => new AuthorizationFieldValueDto
                    {
                        FieldName = ra.FieldName,
                        FieldValue = ra.FieldValue,
                        FromValue = ra.FromValue,
                        ToValue = ra.ToValue
                    }).ToList()
                }).ToList(),
            CreatedOn = role.CreatedOn,
            CreatedBy = role.CreatedBy
        };
    }

    public async Task<RoleDetailDto?> GetRoleByNameAsync(string roleName)
    {
        var role = await _context.Roles
            .Include(r => r.RoleAuthorizations)
                .ThenInclude(ra => ra.Authorization)
            .Include(r => r.UserRoles)
            .FirstOrDefaultAsync(r => r.RoleName == roleName.ToUpper());

        if (role == null)
        {
            return null;
        }

        return new RoleDetailDto
        {
            Id = role.Id,
            RoleName = role.RoleName,
            DisplayName = role.DisplayName,
            Description = role.Description,
            RoleType = role.RoleType.ToString(),
            Module = role.Module,
            IsActive = role.IsActive,
            UserCount = role.UserRoles.Count,
            Authorizations = role.RoleAuthorizations
                .GroupBy(ra => ra.Authorization)
                .Select(g => new AuthorizationSummaryDto
                {
                    Id = g.Key.Id,
                    ObjectCode = g.Key.ObjectCode,
                    DisplayName = g.Key.DisplayName,
                    Module = g.Key.Module,
                    FieldValues = g.Select(ra => new AuthorizationFieldValueDto
                    {
                        FieldName = ra.FieldName,
                        FieldValue = ra.FieldValue,
                        FromValue = ra.FromValue,
                        ToValue = ra.ToValue
                    }).ToList()
                }).ToList(),
            CreatedOn = role.CreatedOn,
            CreatedBy = role.CreatedBy
        };
    }

    public async Task<List<RoleListDto>> GetAllRolesAsync(string? module = null, bool? isActive = null)
    {
        var query = _context.Roles
            .Include(r => r.UserRoles)
            .Include(r => r.RoleAuthorizations)
            .AsQueryable();

        if (!string.IsNullOrEmpty(module))
        {
            query = query.Where(r => r.Module == module.ToUpper());
        }

        if (isActive.HasValue)
        {
            query = query.Where(r => r.IsActive == isActive.Value);
        }

        var roles = await query
            .OrderBy(r => r.Module)
            .ThenBy(r => r.DisplayName)
            .ToListAsync();

        return roles.Select(r => new RoleListDto
        {
            Id = r.Id,
            RoleName = r.RoleName,
            DisplayName = r.DisplayName,
            RoleType = r.RoleType.ToString(),
            Module = r.Module ?? string.Empty,
            IsActive = r.IsActive,
            UserCount = r.UserRoles.Count,
            AuthorizationCount = r.RoleAuthorizations.Select(ra => ra.AuthorizationId).Distinct().Count()
        }).ToList();
    }

    public async Task<RoleDetailDto> UpdateRoleAsync(long roleId, UpdateRoleRequest request, string currentUsername)
    {
        _logger.LogInformation("Updating role {RoleId} by {User}", roleId, currentUsername);

        var role = await _context.Roles.FindAsync(roleId);
        if (role == null)
        {
            throw new KeyNotFoundException($"Role with ID {roleId} not found");
        }

        // Standard SAP roles should not be modified
        if (role.RoleType == RoleType.Standard && role.RoleName.StartsWith("SAP_"))
        {
            throw new InvalidOperationException("Standard SAP roles cannot be modified");
        }

        if (!string.IsNullOrEmpty(request.DisplayName))
        {
            role.DisplayName = request.DisplayName;
        }

        if (request.Description != null)
        {
            role.Description = request.Description;
        }

        if (!string.IsNullOrEmpty(request.Module))
        {
            role.Module = request.Module.ToUpper();
        }

        if (request.IsActive.HasValue)
        {
            role.IsActive = request.IsActive.Value;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Role {RoleId} updated successfully", roleId);

        return await GetRoleByIdAsync(roleId);
    }

    public async Task<bool> DeleteRoleAsync(long roleId)
    {
        _logger.LogInformation("Deleting role {RoleId}", roleId);

        var role = await _context.Roles
            .Include(r => r.UserRoles)
            .Include(r => r.RoleAuthorizations)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role == null)
        {
            return false;
        }

        // Cannot delete standard SAP roles
        if (role.RoleType == RoleType.Standard && role.RoleName.StartsWith("SAP_"))
        {
            throw new InvalidOperationException("Standard SAP roles cannot be deleted");
        }

        // Cannot delete if assigned to users
        if (role.UserRoles.Any())
        {
            throw new InvalidOperationException($"Cannot delete role '{role.RoleName}' as it is assigned to {role.UserRoles.Count} user(s)");
        }

        // Remove authorization assignments
        _context.RoleAuthorizations.RemoveRange(role.RoleAuthorizations);

        // Remove role
        _context.Roles.Remove(role);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Role {RoleId} deleted successfully", roleId);

        return true;
    }

    public async Task<bool> ActivateRoleAsync(long roleId, string currentUsername)
    {
        var role = await _context.Roles.FindAsync(roleId);
        if (role == null)
        {
            return false;
        }

        role.IsActive = true;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Role {RoleId} activated by {User}", roleId, currentUsername);
        return true;
    }

    public async Task<bool> DeactivateRoleAsync(long roleId, string currentUsername)
    {
        var role = await _context.Roles.FindAsync(roleId);
        if (role == null)
        {
            return false;
        }

        // Cannot deactivate SAP_ALL
        if (role.RoleName == "SAP_ALL")
        {
            throw new InvalidOperationException("SAP_ALL role cannot be deactivated");
        }

        role.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Role {RoleId} deactivated by {User}", roleId, currentUsername);
        return true;
    }

    public async Task<UserRoleDto> AssignRoleToUserAsync(AssignRoleToUserRequest request, string currentUsername)
    {
        _logger.LogInformation("Assigning role {RoleId} to user {UserId} by {User}", request.RoleId, request.UserId, currentUsername);

        // Validate user exists
        var user = await _context.Users.FindAsync(request.UserId);
        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID {request.UserId} not found");
        }

        // Validate role exists and is active
        var role = await _context.Roles.FindAsync(request.RoleId);
        if (role == null)
        {
            throw new KeyNotFoundException($"Role with ID {request.RoleId} not found");
        }

        if (!role.IsActive)
        {
            throw new InvalidOperationException($"Role '{role.RoleName}' is not active");
        }

        // Check if already assigned
        var existingAssignment = await _context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == request.UserId && ur.RoleId == request.RoleId);

        if (existingAssignment != null)
        {
            throw new InvalidOperationException($"Role '{role.RoleName}' is already assigned to user '{user.Username}'");
        }

        var userRole = new UserRole
        {
            UserId = request.UserId,
            RoleId = request.RoleId,
            ValidFrom = request.ValidFrom ?? DateTime.UtcNow,
            ValidTo = request.ValidTo,
            ClientId = "001" // TODO: Get from context
        };

        _context.UserRoles.Add(userRole);
        await _context.SaveChangesAsync();

        // Log activity
        var activity = new UserActivity
        {
            UserId = request.UserId,
            Username = user.Username,
            ActivityType = ActivityType.RoleAssignment,
            Description = $"Role '{role.RoleName}' assigned by {currentUsername}",
            Timestamp = DateTime.UtcNow,
            IsSuccessful = true,
            ClientId = "001"
        };
        _context.UserActivities.Add(activity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Role {RoleId} assigned to user {UserId} successfully", request.RoleId, request.UserId);

        return new UserRoleDto
        {
            Id = userRole.Id,
            UserId = user.Id,
            Username = user.Username,
            FullName = $"{user.FirstName} {user.LastName}",
            RoleId = role.Id,
            RoleName = role.RoleName,
            RoleDisplayName = role.DisplayName,
            ValidFrom = userRole.ValidFrom,
            ValidTo = userRole.ValidTo,
            AssignedOn = userRole.CreatedOn,
            AssignedBy = userRole.CreatedBy
        };
    }

    public async Task<bool> RemoveRoleFromUserAsync(RemoveRoleFromUserRequest request, string currentUsername)
    {
        _logger.LogInformation("Removing role {RoleId} from user {UserId} by {User}", request.RoleId, request.UserId, currentUsername);

        var userRole = await _context.UserRoles
            .Include(ur => ur.Role)
            .Include(ur => ur.User)
            .FirstOrDefaultAsync(ur => ur.UserId == request.UserId && ur.RoleId == request.RoleId);

        if (userRole == null)
        {
            return false;
        }

        // Prevent removing SAP_ALL from last admin
        if (userRole.Role.RoleName == "SAP_ALL")
        {
            var sapAllCount = await _context.UserRoles
                .Include(ur => ur.Role)
                .CountAsync(ur => ur.Role.RoleName == "SAP_ALL" && ur.User.Status == UserStatus.Active);

            if (sapAllCount <= 1)
            {
                throw new InvalidOperationException("Cannot remove SAP_ALL role from the last active administrator");
            }
        }

        _context.UserRoles.Remove(userRole);
        await _context.SaveChangesAsync();

        // Log activity
        var activity = new UserActivity
        {
            UserId = request.UserId,
            Username = userRole.User.Username,
            ActivityType = ActivityType.RoleAssignment,
            Description = $"Role '{userRole.Role.RoleName}' removed by {currentUsername}",
            Timestamp = DateTime.UtcNow,
            IsSuccessful = true,
            ClientId = "001"
        };
        _context.UserActivities.Add(activity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Role {RoleId} removed from user {UserId} successfully", request.RoleId, request.UserId);

        return true;
    }

    public async Task<List<UserRoleDto>> GetUserRolesAsync(long userId)
    {
        var userRoles = await _context.UserRoles
            .Include(ur => ur.User)
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userId)
            .OrderBy(ur => ur.Role.DisplayName)
            .ToListAsync();

        return userRoles.Select(ur => new UserRoleDto
        {
            Id = ur.Id,
            UserId = ur.User.Id,
            Username = ur.User.Username,
            FullName = $"{ur.User.FirstName} {ur.User.LastName}",
            RoleId = ur.Role.Id,
            RoleName = ur.Role.RoleName,
            RoleDisplayName = ur.Role.DisplayName,
            ValidFrom = ur.ValidFrom,
            ValidTo = ur.ValidTo,
            AssignedOn = ur.CreatedOn,
            AssignedBy = ur.CreatedBy
        }).ToList();
    }

    public async Task<List<UserRoleDto>> GetRoleUsersAsync(long roleId)
    {
        var userRoles = await _context.UserRoles
            .Include(ur => ur.User)
            .Include(ur => ur.Role)
            .Where(ur => ur.RoleId == roleId)
            .OrderBy(ur => ur.User.Username)
            .ToListAsync();

        return userRoles.Select(ur => new UserRoleDto
        {
            Id = ur.Id,
            UserId = ur.User.Id,
            Username = ur.User.Username,
            FullName = $"{ur.User.FirstName} {ur.User.LastName}",
            RoleId = ur.Role.Id,
            RoleName = ur.Role.RoleName,
            RoleDisplayName = ur.Role.DisplayName,
            ValidFrom = ur.ValidFrom,
            ValidTo = ur.ValidTo,
            AssignedOn = ur.CreatedOn,
            AssignedBy = ur.CreatedBy
        }).ToList();
    }

    public async Task<bool> IsRoleAssignedToUserAsync(long userId, long roleId)
    {
        return await _context.UserRoles
            .AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
    }

    public async Task<AuthorizationSummaryDto> AssignAuthorizationAsync(AssignAuthorizationRequest request, string currentUsername)
    {
        _logger.LogInformation("Assigning authorization {AuthorizationId} to role {RoleId} by {User}", 
            request.AuthorizationId, request.RoleId, currentUsername);

        // Validate role exists
        var role = await _context.Roles.FindAsync(request.RoleId);
        if (role == null)
        {
            throw new KeyNotFoundException($"Role with ID {request.RoleId} not found");
        }

        // Validate authorization exists
        var authorization = await _context.Authorizations
            .Include(a => a.Fields)
            .FirstOrDefaultAsync(a => a.Id == request.AuthorizationId);

        if (authorization == null)
        {
            throw new KeyNotFoundException($"Authorization with ID {request.AuthorizationId} not found");
        }

        // Remove existing assignments for this role and authorization
        var existingAssignments = await _context.RoleAuthorizations
            .Where(ra => ra.RoleId == request.RoleId && ra.AuthorizationId == request.AuthorizationId)
            .ToListAsync();

        _context.RoleAuthorizations.RemoveRange(existingAssignments);

        // Add new assignments
        foreach (var fieldValue in request.FieldValues)
        {
            var roleAuth = new RoleAuthorization
            {
                RoleId = request.RoleId,
                AuthorizationId = request.AuthorizationId,
                FieldName = fieldValue.FieldName,
                FieldValue = fieldValue.FieldValue,
                FromValue = fieldValue.FromValue,
                ToValue = fieldValue.ToValue,
                IsActive = true,
                ClientId = "001"
            };

            _context.RoleAuthorizations.Add(roleAuth);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Authorization {AuthorizationId} assigned to role {RoleId} successfully", 
            request.AuthorizationId, request.RoleId);

        return new AuthorizationSummaryDto
        {
            Id = authorization.Id,
            ObjectCode = authorization.ObjectCode,
            DisplayName = authorization.DisplayName,
            Module = authorization.Module,
            FieldValues = request.FieldValues
        };
    }

    public async Task<bool> RemoveAuthorizationFromRoleAsync(long roleId, long authorizationId)
    {
        _logger.LogInformation("Removing authorization {AuthorizationId} from role {RoleId}", authorizationId, roleId);

        var roleAuths = await _context.RoleAuthorizations
            .Where(ra => ra.RoleId == roleId && ra.AuthorizationId == authorizationId)
            .ToListAsync();

        if (!roleAuths.Any())
        {
            return false;
        }

        _context.RoleAuthorizations.RemoveRange(roleAuths);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Authorization {AuthorizationId} removed from role {RoleId} successfully", 
            authorizationId, roleId);

        return true;
    }

    public async Task<List<AuthorizationSummaryDto>> GetRoleAuthorizationsAsync(long roleId)
    {
        var roleAuths = await _context.RoleAuthorizations
            .Include(ra => ra.Authorization)
            .Where(ra => ra.RoleId == roleId)
            .ToListAsync();

        return roleAuths
            .GroupBy(ra => ra.Authorization)
            .Select(g => new AuthorizationSummaryDto
            {
                Id = g.Key.Id,
                ObjectCode = g.Key.ObjectCode,
                DisplayName = g.Key.DisplayName,
                Module = g.Key.Module,
                FieldValues = g.Select(ra => new AuthorizationFieldValueDto
                {
                    FieldName = ra.FieldName,
                    FieldValue = ra.FieldValue,
                    FromValue = ra.FromValue,
                    ToValue = ra.ToValue
                }).ToList()
            }).ToList();
    }

    public async Task<bool> RoleExistsAsync(long roleId)
    {
        return await _context.Roles.AnyAsync(r => r.Id == roleId);
    }

    public async Task<bool> RoleNameExistsAsync(string roleName)
    {
        return await _context.Roles.AnyAsync(r => r.RoleName == roleName.ToUpper());
    }

    public async Task<bool> CanDeleteRoleAsync(long roleId)
    {
        var role = await _context.Roles
            .Include(r => r.UserRoles)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role == null)
        {
            return false;
        }

        // Cannot delete standard SAP roles
        if (role.RoleType == RoleType.Standard && role.RoleName.StartsWith("SAP_"))
        {
            return false;
        }

        // Cannot delete if assigned to users
        if (role.UserRoles.Any())
        {
            return false;
        }

        return true;
    }
}
