using DigitalisationERP.Application.DTOs.Auth;
using DigitalisationERP.Application.DTOs.Email;
using DigitalisationERP.Application.DTOs.Identity.Responses;
using DigitalisationERP.Application.Identity.Commands;
using DigitalisationERP.Application.Identity.Queries;
using DigitalisationERP.Application.Interfaces;
using DigitalisationERP.Infrastructure.Services;
using DigitalisationERP.Infrastructure.Data;
using DigitalisationERP.Core.Entities.Auth;
using Microsoft.EntityFrameworkCore;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BCrypt.Net;
using AuthLoginResponse = DigitalisationERP.Application.DTOs.Auth.LoginResponse;
using System.Security.Claims;

namespace DigitalisationERP.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;
    private readonly ApplicationDbContext _context;

    public AuthController(IMediator mediator, IAuthService authService, ILogger<AuthController> logger, ApplicationDbContext context)
    {
        _mediator = mediator;
        _authService = authService;
        _logger = logger;
        _context = context;
    }

    private List<string> GetRoles()
        => User.FindAll(ClaimTypes.Role).Select(r => r.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

    private bool IsUserAdmin()
    {
        var roles = GetRoles();
        return roles.Any(r => r.Equals("S_USER", StringComparison.OrdinalIgnoreCase))
               || roles.Any(r => r.Equals("Z_MAINTENANCE", StringComparison.OrdinalIgnoreCase))
               || roles.Any(r => r.Equals("Z_MAINT_TECH", StringComparison.OrdinalIgnoreCase));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthLoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Login attempt for username: {Username}", request.Username);

            // Use IAuthService directly instead of MediatR
            var (success, message, response) = await _authService.LoginAsync(request);

            if (!success)
            {
                _logger.LogWarning("Login failed for username: {Username} - Error: {Error}", request.Username, message);
                return BadRequest(new { error = message, message = "Identifiants invalides" });
            }

            _logger.LogInformation("User {Username} logged in successfully", request.Username);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during login for username: {Username}", request.Username);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    [HttpGet("create-test-account")]
    [AllowAnonymous]
    public async Task<ActionResult> CreateTestAccount()
    {
        try
        {
            var result = await _authService.RegisterAsync(
                new DigitalisationERP.Application.DTOs.Auth.RegisterRequest
                {
                    Username = "admin-test",
                    Email = "test.user@example.com",
                    Password = "ChangeMe123!",
                    ConfirmPassword = "ChangeMe123!",
                    FirstName = "Admin",
                    LastName = "Test"
                }
            );

            return Ok(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message, error = ex.ToString() });
        }
    }

    [HttpGet("debug-user")]
    [AllowAnonymous]
    public async Task<ActionResult> DebugUser([FromQuery] string? q = null)
    {
        try
        {
            var query = string.IsNullOrWhiteSpace(q) ? "admin@erp.local" : q.Trim();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == query || u.Email == query);

            if (user == null)
            {
                var totalUsers = await _context.Users.CountAsync();
                return Ok(new { found = false, query, totalUsers, message = "User not found" });
            }

            return Ok(new 
            { 
                found = true,
                query,
                username = user.Username,
                email = user.Email,
                emailVerified = user.EmailVerified,
                status = user.Status.ToString(),
                lockedUntil = user.LockedUntil,
                failedLoginAttempts = user.FailedLoginAttempts,
                passwordHashLength = user.PasswordHash?.Length ?? 0,
                passwordHash = user.PasswordHash
            });
        }
        catch (Exception ex)
        {
            return Ok(new { error = ex.Message });
        }
    }

    [HttpGet("test-users")]
    [AllowAnonymous]
    public ActionResult GetTestUsers()
    {
        var users = _authService.GetUserByIdAsync(1).Result; // Just to get access to context
        return Ok(new { message = "Check logs for user list" });
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Registration attempt for email: {Email}", request.Email);

            // Use AuthService directly for registration
            var result = await _authService.RegisterAsync(request);

            if (!result.Success)
            {
                _logger.LogWarning("Registration failed for email: {Email} - Error: {Message}", request.Email, result.Message);
                return BadRequest(new { error = result.Message });
            }

            _logger.LogInformation("User {Email} registered successfully", request.Email);
            
            // Return response with user data
            var response = new RegisterResponse
            { 
                UserId = result.User?.Id ?? 0,
                Username = result.User?.Username ?? request.Username,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Message = "Registration successful. Please verify your email."
            };
            
            return CreatedAtAction(nameof(Register), response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during registration for email: {Email}", request.Email);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    /// <summary>
    /// Admin creates a user (internal system; no external emails required)
    /// Desktop legacy endpoint used by CreateUserViewModel
    /// </summary>
    [HttpPost("create-user")]
    [Authorize]
    public async Task<ActionResult> CreateUser([FromBody] CreateUserAdminRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!IsUserAdmin())
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { error = "Username, Email et Password sont requis" });
            }

            var username = request.Username.Trim();
            var email = request.Email.Trim();

            if (await _context.Users.AnyAsync(u => u.Username == username, cancellationToken))
            {
                return BadRequest(new { error = "Username existe déjà" });
            }

            if (await _context.Users.AnyAsync(u => u.Email == email, cancellationToken))
            {
                return BadRequest(new { error = "Email existe déjà" });
            }

            var (firstName, lastName) = SplitName(request.FullName);

            var user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                FirstName = firstName,
                LastName = lastName,
                PhoneNumber = request.PhoneNumber,
                Department = request.Department,
                EmailVerified = true,
                EmailVerifiedAt = DateTime.UtcNow,
                Status = UserStatus.Active,
                UserType = UserType.Dialog,
                ValidFrom = DateTime.UtcNow,
                PasswordLastChanged = DateTime.UtcNow,
                MustChangePassword = false,
                FailedLoginAttempts = 0,
                CreatedOn = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? string.Empty
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);

            var roleNames = (request.Roles ?? new List<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (roleNames.Count > 0)
            {
                var roles = await _context.Roles.Where(r => roleNames.Contains(r.RoleName)).ToListAsync(cancellationToken);
                foreach (var role in roles)
                {
                    _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, IsActive = true, ValidFrom = DateTime.UtcNow });
                }
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Ok(new { success = true, userId = user.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user via /api/auth/create-user");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    private static (string FirstName, string LastName) SplitName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return (string.Empty, string.Empty);
        }

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return (parts[0], string.Empty);
        }

        return (parts[0], string.Join(" ", parts.Skip(1)));
    }

    public sealed class CreateUserAdminRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Department { get; set; }
        public List<string>? Roles { get; set; }
        public bool SendCredentials { get; set; } = false;
    }

    [HttpGet("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest(new { error = "Token de vérification manquant" });
            }

            _logger.LogInformation("Email verification attempt with token");
            var result = await _authService.VerifyEmailAsync(token);

            if (!result.Success)
            {
                _logger.LogWarning("Email verification failed - Error: {Message}", result.Message);
                return BadRequest(new { error = result.Message });
            }

            _logger.LogInformation("Email verified successfully");
            return Ok(new { message = "Email vérifié avec succès" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during email verification");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthLoginResponse>> RefreshToken(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Token refresh attempt");

            // Extract accessToken from Authorization header if available
            var accessToken = Request.Headers.Authorization.ToString().Replace("Bearer ", "");
            var command = new RefreshTokenCommand(request.RefreshToken, accessToken);
            var result = await _mediator.Send(command, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Token refresh failed - Error: {Error}", result.Error);
                return Unauthorized(new { error = result.Error });
            }

            _logger.LogInformation("Token refreshed successfully");
            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during token refresh");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    /// <summary>
    /// Déconnecte l'utilisateur et termine la session
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult> Logout(CancellationToken cancellationToken)
    {
        try
        {
            var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            
            if (!long.TryParse(userIdClaim, out var userId))
            {
                _logger.LogWarning("Logout attempt with invalid user ID in claims");
                return Unauthorized();
            }

            _logger.LogInformation("Logout attempt for user ID: {UserId}", userId);

            var command = new LogoutCommand(userId);
            var result = await _mediator.Send(command, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Logout failed for user ID: {UserId} - Error: {Error}", userId, result.Error);
                return BadRequest(new { error = result.Error });
            }

            _logger.LogInformation("User {UserId} logged out successfully", userId);
            return Ok(new { message = "Déconnexion réussie" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during logout");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    /// <summary>
    /// Change le mot de passe de l'utilisateur
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            
            if (!long.TryParse(userIdClaim, out var userId))
            {
                _logger.LogWarning("Change password attempt with invalid user ID in claims");
                return Unauthorized();
            }

            _logger.LogInformation("Change password attempt for user ID: {UserId}", userId);

            var command = new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword);
            var result = await _mediator.Send(command, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Change password failed for user ID: {UserId} - Error: {Error}", userId, result.Error);
                return BadRequest(new { error = result.Error });
            }

            _logger.LogInformation("Password changed successfully for user ID: {UserId}", userId);
            return Ok(new { message = "Mot de passe changé avec succès" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during password change for user ID");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    /// <summary>
    /// Récupère les utilisateurs en attente de vérification (pour S-User)
    /// </summary>
    [HttpGet("pending-verifications")]
    [Authorize]
    public async Task<ActionResult> GetPendingVerifications()
    {
        try
        {
            _logger.LogInformation("Fetching pending verifications");
            
            // TODO: Vérifier que l'utilisateur est S-User
            var pendingUsers = await _authService.GetPendingVerificationsAsync();
            
            return Ok(new { data = pendingUsers });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while fetching pending verifications");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    [HttpGet("pending-users")]
    [Authorize]
    public async Task<ActionResult> GetPendingUsers()
    {
        try
        {
            var pendingUsers = await _authService.GetPendingVerificationsAsync();
            
            // Format response for web UI
            var formattedUsers = pendingUsers.Select(u => new
            {
                id = u.Id,
                email = u.Email,
                firstName = u.FirstName,
                lastName = u.LastName,
                status = u.Status ?? "PendingActivation",
                createdAt = u.LastLogin ?? DateTime.UtcNow
            }).ToList();

            return Ok(formattedUsers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while fetching pending users");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    [HttpPost("approve-user/{userId}")]
    [Authorize]
    public async Task<ActionResult> ApproveUser(long userId, [FromBody] ApprovalRequest request)
    {
        try
        {
            var user = await _authService.GetUserByIdAsync(userId);
            if (user == null)
                return NotFound(new { error = "Utilisateur non trouvé" });

            // Update user status to Active
            user.Status = DigitalisationERP.Core.Entities.Auth.UserStatus.Active;
            user.EmailVerified = true;
            
            // Save changes
            var dbContext = HttpContext.RequestServices.GetService<DigitalisationERP.Infrastructure.Data.ApplicationDbContext>();
            if (dbContext != null)
            {
                dbContext.Set<DigitalisationERP.Core.Entities.Auth.User>().Update(user);
                await dbContext.SaveChangesAsync();
            }

            // Send email if IEmailService is available
            var emailService = HttpContext.RequestServices.GetService<IEmailService>();
            if (emailService != null && !string.IsNullOrEmpty(user.Email))
            {
                var emailRequest = new SendApprovalEmailRequest
                {
                    Email = user.Email,
                    FirstName = user.FirstName,
                    Message = request.Message ?? "Votre compte a été approuvé. Vous pouvez maintenant vous connecter."
                };
                await emailService.SendApprovalEmailAsync(emailRequest);
            }

            return Ok(new { success = true, message = "Utilisateur approuvé avec succès" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while approving user {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    [HttpPost("reject-user/{userId}")]
    [Authorize]
    public async Task<ActionResult> RejectUser(long userId, [FromBody] RejectionRequest request)
    {
        try
        {
            var dbContext = HttpContext.RequestServices.GetService<DigitalisationERP.Infrastructure.Data.ApplicationDbContext>();
            if (dbContext == null)
                return StatusCode(500, new { error = "Service non disponible" });

            var user = await _authService.GetUserByIdAsync(userId);
            if (user == null)
                return NotFound(new { error = "Utilisateur non trouvé" });

            // Delete the user instead of rejecting
            dbContext.Set<DigitalisationERP.Core.Entities.Auth.User>().Remove(user);
            await dbContext.SaveChangesAsync();

            // Send rejection email
            var emailService = HttpContext.RequestServices.GetService<IEmailService>();
            if (emailService != null && !string.IsNullOrEmpty(user.Email))
            {
                var emailRequest = new SendRejectionEmailRequest
                {
                    Email = user.Email,
                    FirstName = user.FirstName,
                    Reason = request.Reason ?? "Votre demande d'inscription a été rejetée."
                };
                await emailService.SendRejectionEmailAsync(emailRequest);
            }

            return Ok(new { success = true, message = "Demande rejetée et email envoyé" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while rejecting user {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Public endpoint for S-User web panel - validates token from URL parameter
    /// Used by suser.html web interface
    /// </summary>
    [HttpGet("pending-users-public")]
    [AllowAnonymous]
    public async Task<ActionResult> GetPendingUsersPublic([FromQuery] string? token = null)
    {
        try
        {
            // No token validation - just return pending users
            var pendingUsers = await _authService.GetPendingVerificationsAsync();
            
            // Format response for web UI
            var formattedUsers = pendingUsers?.Select(u => (object)new
            {
                id = u.Id,
                email = u.Email,
                firstName = u.FirstName,
                lastName = u.LastName,
                status = u.Status ?? "PendingActivation",
                createdAt = u.LastLogin ?? DateTime.UtcNow
            }).ToList() ?? new List<object>();

            return Ok(formattedUsers);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in pending-users-public: {ex.Message}");
            _logger.LogError(ex, "Exception occurred while fetching pending users (public)");
            return Ok(new List<object>()); // Return empty list on error
        }
    }

    /// <summary>
    /// Public endpoint for S-User web panel - approve user with token validation
    /// </summary>
    [HttpPost("approve-user-public/{userId}")]
    [AllowAnonymous]
    public async Task<ActionResult> ApproveUserPublic(long userId, [FromQuery] string? token = null, [FromBody] ApprovalRequest? request = null)
    {
        try
        {
            // No token validation - just process the approval
            var user = await _authService.GetUserByIdAsync(userId);
            if (user == null)
                return NotFound(new { error = "Utilisateur non trouvé" });

            user.Status = DigitalisationERP.Core.Entities.Auth.UserStatus.Active;
            user.EmailVerified = true;
            
            var dbContext = HttpContext.RequestServices.GetService<DigitalisationERP.Infrastructure.Data.ApplicationDbContext>();
            if (dbContext != null)
            {
                dbContext.Set<DigitalisationERP.Core.Entities.Auth.User>().Update(user);
                await dbContext.SaveChangesAsync();
            }

            var emailService = HttpContext.RequestServices.GetService<IEmailService>();
            if (emailService != null && !string.IsNullOrEmpty(user.Email))
            {
                var emailRequest = new SendApprovalEmailRequest
                {
                    Email = user.Email,
                    FirstName = user.FirstName,
                    Message = request?.Message ?? "Votre compte a été approuvé. Vous pouvez maintenant vous connecter."
                };
                await emailService.SendApprovalEmailAsync(emailRequest);
            }

            return Ok(new { success = true, message = "Utilisateur approuvé avec succès" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while approving user {UserId} (public)", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Public endpoint for S-User web panel - reject user with token validation
    /// </summary>
    [HttpPost("reject-user-public/{userId}")]
    [AllowAnonymous]
    public async Task<ActionResult> RejectUserPublic(long userId, [FromQuery] string? token = null, [FromBody] RejectionRequest? request = null)
    {
        try
        {
            // No token validation - just process the rejection
            var dbContext = HttpContext.RequestServices.GetService<DigitalisationERP.Infrastructure.Data.ApplicationDbContext>();
            if (dbContext == null)
                return StatusCode(500, new { success = false, error = "Service non disponible" });

            var user = await _authService.GetUserByIdAsync(userId);
            if (user == null)
                return NotFound(new { success = false, error = "Utilisateur non trouvé" });

            dbContext.Set<DigitalisationERP.Core.Entities.Auth.User>().Remove(user);
            await dbContext.SaveChangesAsync();

            var emailService = HttpContext.RequestServices.GetService<IEmailService>();
            if (emailService != null && !string.IsNullOrEmpty(user.Email))
            {
                var emailRequest = new SendRejectionEmailRequest
                {
                    Email = user.Email,
                    FirstName = user.FirstName,
                    Reason = request?.Reason ?? "Votre demande d'inscription a été rejetée."
                };
                await emailService.SendRejectionEmailAsync(emailRequest);
            }

            return Ok(new { success = true, message = "Demande rejetée et email envoyé" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while rejecting user {UserId} (public)", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, error = ex.Message });
        }
    }
}

public class ApprovalRequest
{
    public string? Message { get; set; }
}

public class RejectionRequest
{
    public string? Reason { get; set; }
}
