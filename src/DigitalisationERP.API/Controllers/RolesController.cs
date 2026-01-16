using DigitalisationERP.Application.DTOs.Role;
using DigitalisationERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DigitalisationERP.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly ILogger<RolesController> _logger;

    public RolesController(IRoleService roleService, ILogger<RolesController> logger)
    {
        _roleService = roleService;
        _logger = logger;
    }

    private string GetCurrentUsername() => User.FindFirst(ClaimTypes.Name)?.Value ?? "system";

    /// <summary>
    /// Get all roles with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<RoleListDto>>> GetAllRoles(
        [FromQuery] string? module = null,
        [FromQuery] bool? isActive = null)
    {
        try
        {
            var roles = await _roleService.GetAllRolesAsync(module, isActive);
            return Ok(roles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving roles");
            return StatusCode(500, new { message = "An error occurred while retrieving roles" });
        }
    }

    /// <summary>
    /// Get role by ID with full details
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<RoleDetailDto>> GetRoleById(long id)
    {
        try
        {
            var role = await _roleService.GetRoleByIdAsync(id);
            return Ok(role);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving role {RoleId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the role" });
        }
    }

    /// <summary>
    /// Get role by name
    /// </summary>
    [HttpGet("by-name/{roleName}")]
    public async Task<ActionResult<RoleDetailDto>> GetRoleByName(string roleName)
    {
        try
        {
            var role = await _roleService.GetRoleByNameAsync(roleName);
            if (role == null)
            {
                return NotFound(new { message = $"Role '{roleName}' not found" });
            }
            return Ok(role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving role {RoleName}", roleName);
            return StatusCode(500, new { message = "An error occurred while retrieving the role" });
        }
    }

    /// <summary>
    /// Create a new role
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<RoleDetailDto>> CreateRole([FromBody] CreateRoleRequest request)
    {
        try
        {
            var currentUser = GetCurrentUsername();
            var role = await _roleService.CreateRoleAsync(request, currentUser);
            return CreatedAtAction(nameof(GetRoleById), new { id = role.Id }, role);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role");
            return StatusCode(500, new { message = "An error occurred while creating the role" });
        }
    }

    /// <summary>
    /// Update an existing role
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<RoleDetailDto>> UpdateRole(long id, [FromBody] UpdateRoleRequest request)
    {
        try
        {
            var currentUser = GetCurrentUsername();
            var role = await _roleService.UpdateRoleAsync(id, request, currentUser);
            return Ok(role);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role {RoleId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the role" });
        }
    }

    /// <summary>
    /// Delete a role
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteRole(long id)
    {
        try
        {
            var result = await _roleService.DeleteRoleAsync(id);
            if (!result)
            {
                return NotFound(new { message = "Role not found" });
            }
            return Ok(new { message = "Role deleted successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting role {RoleId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the role" });
        }
    }

    /// <summary>
    /// Activate a role
    /// </summary>
    [HttpPost("{id}/activate")]
    public async Task<ActionResult> ActivateRole(long id)
    {
        try
        {
            var currentUser = GetCurrentUsername();
            var result = await _roleService.ActivateRoleAsync(id, currentUser);
            if (!result)
            {
                return NotFound(new { message = "Role not found" });
            }
            return Ok(new { message = "Role activated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating role {RoleId}", id);
            return StatusCode(500, new { message = "An error occurred while activating the role" });
        }
    }

    /// <summary>
    /// Deactivate a role
    /// </summary>
    [HttpPost("{id}/deactivate")]
    public async Task<ActionResult> DeactivateRole(long id)
    {
        try
        {
            var currentUser = GetCurrentUsername();
            var result = await _roleService.DeactivateRoleAsync(id, currentUser);
            if (!result)
            {
                return NotFound(new { message = "Role not found" });
            }
            return Ok(new { message = "Role deactivated successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating role {RoleId}", id);
            return StatusCode(500, new { message = "An error occurred while deactivating the role" });
        }
    }

    /// <summary>
    /// Assign a role to a user
    /// </summary>
    [HttpPost("assign")]
    public async Task<ActionResult<UserRoleDto>> AssignRoleToUser([FromBody] AssignRoleToUserRequest request)
    {
        try
        {
            var currentUser = GetCurrentUsername();
            var userRole = await _roleService.AssignRoleToUserAsync(request, currentUser);
            return Ok(userRole);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning role to user");
            return StatusCode(500, new { message = "An error occurred while assigning the role" });
        }
    }

    /// <summary>
    /// Remove a role from a user
    /// </summary>
    [HttpPost("remove")]
    public async Task<ActionResult> RemoveRoleFromUser([FromBody] RemoveRoleFromUserRequest request)
    {
        try
        {
            var currentUser = GetCurrentUsername();
            var result = await _roleService.RemoveRoleFromUserAsync(request, currentUser);
            if (!result)
            {
                return NotFound(new { message = "Role assignment not found" });
            }
            return Ok(new { message = "Role removed from user successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing role from user");
            return StatusCode(500, new { message = "An error occurred while removing the role" });
        }
    }

    /// <summary>
    /// Get all roles assigned to a specific user
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<List<UserRoleDto>>> GetUserRoles(long userId)
    {
        try
        {
            var userRoles = await _roleService.GetUserRolesAsync(userId);
            return Ok(userRoles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving roles for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred while retrieving user roles" });
        }
    }

    /// <summary>
    /// Get all users assigned to a specific role
    /// </summary>
    [HttpGet("{roleId}/users")]
    public async Task<ActionResult<List<UserRoleDto>>> GetRoleUsers(long roleId)
    {
        try
        {
            var roleUsers = await _roleService.GetRoleUsersAsync(roleId);
            return Ok(roleUsers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users for role {RoleId}", roleId);
            return StatusCode(500, new { message = "An error occurred while retrieving role users" });
        }
    }

    /// <summary>
    /// Assign authorization to a role
    /// </summary>
    [HttpPost("{roleId}/authorizations")]
    public async Task<ActionResult<AuthorizationSummaryDto>> AssignAuthorization(
        long roleId,
        [FromBody] AssignAuthorizationRequest request)
    {
        try
        {
            // Ensure roleId matches
            request.RoleId = roleId;

            var currentUser = GetCurrentUsername();
            var authorization = await _roleService.AssignAuthorizationAsync(request, currentUser);
            return Ok(authorization);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning authorization to role");
            return StatusCode(500, new { message = "An error occurred while assigning the authorization" });
        }
    }

    /// <summary>
    /// Remove authorization from a role
    /// </summary>
    [HttpDelete("{roleId}/authorizations/{authorizationId}")]
    public async Task<ActionResult> RemoveAuthorization(long roleId, long authorizationId)
    {
        try
        {
            var result = await _roleService.RemoveAuthorizationFromRoleAsync(roleId, authorizationId);
            if (!result)
            {
                return NotFound(new { message = "Authorization assignment not found" });
            }
            return Ok(new { message = "Authorization removed from role successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing authorization from role");
            return StatusCode(500, new { message = "An error occurred while removing the authorization" });
        }
    }

    /// <summary>
    /// Get all authorizations assigned to a role
    /// </summary>
    [HttpGet("{roleId}/authorizations")]
    public async Task<ActionResult<List<AuthorizationSummaryDto>>> GetRoleAuthorizations(long roleId)
    {
        try
        {
            var authorizations = await _roleService.GetRoleAuthorizationsAsync(roleId);
            return Ok(authorizations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving authorizations for role {RoleId}", roleId);
            return StatusCode(500, new { message = "An error occurred while retrieving role authorizations" });
        }
    }

    /// <summary>
    /// Check if a role can be deleted
    /// </summary>
    [HttpGet("{roleId}/can-delete")]
    public async Task<ActionResult<bool>> CanDeleteRole(long roleId)
    {
        try
        {
            var canDelete = await _roleService.CanDeleteRoleAsync(roleId);
            return Ok(new { canDelete, roleId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if role can be deleted");
            return StatusCode(500, new { message = "An error occurred while checking role delete status" });
        }
    }
}
