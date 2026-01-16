using DigitalisationERP.Application.DTOs.Identity.Requests;
using DigitalisationERP.Application.DTOs.Identity.Responses;
using DigitalisationERP.Application.DTOs.Identity;
using DigitalisationERP.Application.Identity.Commands;
using DigitalisationERP.Application.Identity.Queries;
using DigitalisationERP.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalisationERP.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RoleController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<RoleController> _logger;

    public RoleController(IMediator mediator, ILogger<RoleController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Récupère la liste paginée des rôles
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<RoleDto>>> ListRoles(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching roles list - Page: {PageNumber}, PageSize: {PageSize}", pageNumber, pageSize);

            var query = new ListRolesQuery(pageNumber, pageSize);
            var result = await _mediator.Send(query, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to fetch roles list - Error: {Error}", result.Error);
                return BadRequest(new { error = result.Error });
            }

            if (result.Value == null)
            {
                return Ok(new PaginatedResult<RoleDto>
                {
                    Items = new List<RoleDto>(),
                    TotalCount = 0,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                });
            }

            _logger.LogInformation("Roles list fetched successfully - Total: {Total}", result.Value.TotalCount);
            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while fetching roles list");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    /// <summary>
    /// Récupère les détails d'un rôle spécifique
    /// </summary>
    [HttpGet("{roleId}")]
    public async Task<ActionResult<RoleDto>> GetRole(
        long roleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching role details - Role ID: {RoleId}", roleId);

            var query = new GetRoleQuery(roleId);
            var result = await _mediator.Send(query, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Role not found - Role ID: {RoleId}", roleId);
                return NotFound(new { error = "Rôle non trouvé" });
            }

            if (result.Value == null)
            {
                _logger.LogWarning("Role query succeeded but returned null - Role ID: {RoleId}", roleId);
                return NotFound(new { error = "Rôle non trouvé" });
            }

            _logger.LogInformation("Role details fetched successfully - Role ID: {RoleId}", roleId);
            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while fetching role {RoleId}", roleId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    /// <summary>
    /// Crée un nouveau rôle (Admin only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ROLE_DIRECTOR")]
    public async Task<ActionResult<RoleDto>> CreateRole(
        RoleCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating new role - Name: {RoleName}", request.Name);

            var command = new CreateRoleCommand(request.Name, request.Description, request.ParentRoleId);
            var result = await _mediator.Send(command, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to create role - Name: {RoleName}, Error: {Error}", request.Name, result.Error);
                return BadRequest(new { error = result.Error });
            }

            _logger.LogInformation("Role created successfully - Name: {RoleName}", request.Name);
            var createdRoleId = result.Value;

            var getRoleResult = await _mediator.Send(new GetRoleQuery(createdRoleId), cancellationToken);
            if (getRoleResult.IsSuccess && getRoleResult.Value != null)
            {
                return CreatedAtAction(nameof(GetRole), new { roleId = createdRoleId }, getRoleResult.Value);
            }

            return CreatedAtAction(nameof(GetRole), new { roleId = createdRoleId }, new { roleId = createdRoleId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while creating role - Name: {RoleName}", request.Name);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    /// <summary>
    /// Modifie un rôle existant (Admin only)
    /// </summary>
    [HttpPut("{roleId}")]
    [Authorize(Roles = "ROLE_DIRECTOR")]
    public async Task<ActionResult> UpdateRole(
        long roleId,
        RoleUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating role - Role ID: {RoleId}", roleId);

            var command = new UpdateRoleCommand(roleId, request.Name, request.Description, request.ParentRoleId);
            var result = await _mediator.Send(command, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to update role - Role ID: {RoleId}, Error: {Error}", roleId, result.Error);
                return BadRequest(new { error = result.Error });
            }

            _logger.LogInformation("Role updated successfully - Role ID: {RoleId}", roleId);
            return Ok(new { success = true, message = "Rôle modifié avec succès" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while updating role {RoleId}", roleId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    /// <summary>
    /// Récupère les utilisateurs ayant un rôle spécifique (pagination)
    /// </summary>
    [HttpGet("{roleId}/users")]
    public async Task<ActionResult<PaginatedResult<UserListDto>>> GetRoleUsers(
        long roleId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching users by role - Role ID: {RoleId}, Page: {PageNumber}", roleId, pageNumber);

            var query = new GetRoleUsersQuery(roleId, pageNumber, pageSize);
            var result = await _mediator.Send(query, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to fetch users by role - Role ID: {RoleId}, Error: {Error}", roleId, result.Error);
                return BadRequest(new { error = result.Error });
            }

            if (result.Value == null)
            {
                return Ok(new PaginatedResult<UserListDto>
                {
                    Items = new List<UserListDto>(),
                    TotalCount = 0,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                });
            }

            _logger.LogInformation("Users by role fetched successfully - Role ID: {RoleId}, Total: {Total}", roleId, result.Value.TotalCount);
            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while fetching users by role {RoleId}", roleId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    /// <summary>
    /// Récupère la hiérarchie des rôles (parents et enfants)
    /// </summary>
    [HttpGet("{roleId}/hierarchy")]
    public async Task<ActionResult<RoleHierarchyDto>> GetRoleHierarchy(
        long roleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching role hierarchy - Role ID: {RoleId}", roleId);

            var query = new GetRoleHierarchyQuery(roleId);
            var result = await _mediator.Send(query, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to fetch role hierarchy - Role ID: {RoleId}, Error: {Error}", roleId, result.Error);
                return BadRequest(new { error = result.Error });
            }

            _logger.LogInformation("Role hierarchy fetched successfully - Role ID: {RoleId}", roleId);
            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while fetching role hierarchy {RoleId}", roleId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }
}
