using DigitalisationERP.Application.DTOs.Identity.Requests;
using DigitalisationERP.Application.DTOs.Identity.Responses;
using DigitalisationERP.Application.DTOs.Identity;
using DigitalisationERP.Application.Identity.Commands;
using DigitalisationERP.Application.Identity.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalisationERP.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuthorizationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuthorizationController> _logger;

    public AuthorizationController(IMediator mediator, ILogger<AuthorizationController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Récupère la liste de tous les objets d'autorisation
    /// </summary>
    [HttpGet("objects")]
    public async Task<ActionResult<List<AuthorizationObjectDto>>> GetAuthorizationObjects(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching authorization objects");

            var query = new ListAuthorizationObjectsQuery();
            var result = await _mediator.Send(query, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to fetch authorization objects - Error: {Error}", result.Error);
                return BadRequest(new { error = result.Error });
            }

            if (result.Value == null)
            {
                return Ok(new List<AuthorizationObjectDto>());
            }

            _logger.LogInformation("Authorization objects fetched successfully - Total: {Total}", result.Value.Count);
            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while fetching authorization objects");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    /// <summary>
    /// Récupère les détails d'un objet d'autorisation spécifique
    /// </summary>
    [HttpGet("objects/{objectId}")]
    public async Task<ActionResult<AuthorizationObjectDto>> GetAuthorizationObject(
        long objectId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching authorization object - Object ID: {ObjectId}", objectId);

            var query = new GetAuthorizationObjectQuery(objectId);
            var result = await _mediator.Send(query, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Authorization object not found - Object ID: {ObjectId}", objectId);
                return NotFound(new { error = "Objet d'autorisation non trouvé" });
            }

            _logger.LogInformation("Authorization object fetched successfully - Object ID: {ObjectId}", objectId);
            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while fetching authorization object {ObjectId}", objectId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    /// <summary>
    /// Crée un nouvel objet d'autorisation (Admin only)
    /// </summary>
    [HttpPost("objects")]
    [Authorize(Roles = "ROLE_DIRECTOR")]
    public async Task<ActionResult<AuthorizationObjectDto>> CreateAuthorizationObject(
        AuthObjectCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating authorization object - Name: {ObjectName}", request.Name);

            var command = new CreateAuthorizationObjectCommand(request.Name, request.Description, request.ModuleName);
            var result = await _mediator.Send(command, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to create authorization object - Name: {ObjectName}, Error: {Error}", 
                    request.Name, result.Error);
                return BadRequest(new { error = result.Error });
            }

            _logger.LogInformation("Authorization object created successfully - Name: {ObjectName}", request.Name);
            return CreatedAtAction(nameof(GetAuthorizationObject), new { objectId = result.Value }, result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while creating authorization object - Name: {ObjectName}", request.Name);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    /// <summary>
    /// Récupère les autorisations d'un rôle spécifique
    /// </summary>
    [HttpGet("role-permissions/{roleId}")]
    public async Task<ActionResult<List<RoleAuthorizationDto>>> GetRolePermissions(
        long roleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching role permissions - Role ID: {RoleId}", roleId);

            var query = new GetRolePermissionsQuery(roleId);
            var result = await _mediator.Send(query, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to fetch role permissions - Role ID: {RoleId}, Error: {Error}", roleId, result.Error);
                return BadRequest(new { error = result.Error });
            }

            if (result.Value == null)
            {
                return Ok(new List<RoleAuthorizationDto>());
            }

            _logger.LogInformation("Role permissions fetched successfully - Role ID: {RoleId}, Total: {Total}", 
                roleId, result.Value.Count);
            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while fetching role permissions {RoleId}", roleId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    /// <summary>
    /// Assigne une autorisation à un rôle (Admin only)
    /// </summary>
    [HttpPost("role-permissions")]
    [Authorize(Roles = "ROLE_DIRECTOR")]
    public async Task<ActionResult> AssignAuthorization(
        RoleAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Assigning authorization - Role ID: {RoleId}, Object ID: {ObjectId}", 
                request.RoleId, request.AuthorizationObjectId);

            var command = new AssignAuthorizationCommand(
                request.RoleId,
                request.AuthorizationObjectId,
                request.CanCreate,
                request.CanRead,
                request.CanUpdate,
                request.CanDelete
            );
            var result = await _mediator.Send(command, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to assign authorization - Role ID: {RoleId}, Error: {Error}", 
                    request.RoleId, result.Error);
                return BadRequest(new { error = result.Error });
            }

            _logger.LogInformation("Authorization assigned successfully - Role ID: {RoleId}", request.RoleId);
            return Ok(new { success = true, message = "Autorisation assignée avec succès" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while assigning authorization");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    /// <summary>
    /// Révoque une autorisation d'un rôle (Admin only)
    /// </summary>
    [HttpDelete("role-permissions/{roleAuthId}")]
    [Authorize(Roles = "ROLE_DIRECTOR")]
    public async Task<ActionResult> RevokeAuthorization(
        long roleAuthId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Revoking authorization - Role Authorization ID: {RoleAuthId}", roleAuthId);

            var command = new RevokeAuthorizationCommand(roleAuthId);
            var result = await _mediator.Send(command, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to revoke authorization - Role Auth ID: {RoleAuthId}, Error: {Error}", 
                    roleAuthId, result.Error);
                return BadRequest(new { error = result.Error });
            }

            _logger.LogInformation("Authorization revoked successfully - Role Authorization ID: {RoleAuthId}", roleAuthId);
            return Ok(new { success = true, message = "Autorisation révoquée avec succès" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while revoking authorization {RoleAuthId}", roleAuthId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }
}
