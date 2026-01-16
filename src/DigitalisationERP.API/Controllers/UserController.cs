using DigitalisationERP.Infrastructure.Data;
using DigitalisationERP.Core.Entities.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DigitalisationERP.API.Controllers;

[ApiController]
[Route("api/user")]
[Route("api/users")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserController> _logger;

    public UserController(ApplicationDbContext context, ILogger<UserController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private List<string> GetRoles()
        => User.FindAll(ClaimTypes.Role).Select(r => r.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

    private bool IsUserAdmin()
    {
        var roles = GetRoles();
        // Defense in depth: only allow S_USER and Maintenance IT style roles.
        return roles.Any(r => r.Equals("S_USER", StringComparison.OrdinalIgnoreCase))
               || roles.Any(r => r.Equals("Z_MAINTENANCE", StringComparison.OrdinalIgnoreCase))
               || roles.Any(r => r.Equals("Z_MAINT_TECH", StringComparison.OrdinalIgnoreCase));
    }

    private string GetActorName()
        => User.Identity?.Name
           ?? User.FindFirstValue(ClaimTypes.Name)
           ?? User.FindFirstValue("username")
           ?? string.Empty;

    private static UserListItemDto MapUser(User user)
    {
        var roles = user.UserRoles
            .Where(ur => ur.IsActive && (ur.ValidTo == null || ur.ValidTo >= DateTime.UtcNow))
            .Select(ur => ur.Role.RoleName)
            .Distinct()
            .OrderBy(r => r)
            .ToList();

        return new UserListItemDto
        {
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Department = user.Department,
            PhoneNumber = user.PhoneNumber,
            Roles = roles,
            IsActive = user.Status == UserStatus.Active,
            IsLocked = user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow,
            LastLoginDate = user.LastLogin,
            CreatedDate = user.CreatedOn == default ? DateTime.UtcNow : user.CreatedOn
        };
    }

    [HttpGet]
    public async Task<ActionResult<UserListResponse>> ListUsers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _context.Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(u =>
                    u.Username.Contains(search) ||
                    u.Email.Contains(search) ||
                    u.FirstName.Contains(search) ||
                    u.LastName.Contains(search));
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var users = await query
                .OrderBy(u => u.Username)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return Ok(new UserListResponse
            {
                Items = users.Select(MapUser).ToList(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while fetching users list");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    [HttpGet("{userId}")]
    public async Task<ActionResult<UserDetailDto>> GetUser(
        long userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (user == null)
            {
                return NotFound(new { error = "Utilisateur non trouvé" });
            }

            var dto = new UserDetailDto(MapUser(user))
            {
                Status = user.Status.ToString(),
                EmailVerified = user.EmailVerified,
                ValidFrom = user.ValidFrom,
                ValidTo = user.ValidTo,
                LockedUntil = user.LockedUntil
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while fetching user {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    [HttpPost]
    public async Task<ActionResult<UserListItemDto>> CreateUser(
        [FromBody] CreateUserAdminRequest request,
        CancellationToken cancellationToken = default)
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

            var user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                FirstName = request.FirstName?.Trim() ?? string.Empty,
                LastName = request.LastName?.Trim() ?? string.Empty,
                PhoneNumber = request.PhoneNumber?.Trim(),
                Department = request.Department?.Trim(),
                EmailVerified = true,
                EmailVerifiedAt = DateTime.UtcNow,
                Status = UserStatus.Active,
                UserType = UserType.Dialog,
                ValidFrom = DateTime.UtcNow,
                PasswordLastChanged = DateTime.UtcNow,
                MustChangePassword = false,
                FailedLoginAttempts = 0,
                CreatedOn = DateTime.UtcNow,
                CreatedBy = GetActorName()
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);

            if (request.Roles != null && request.Roles.Count > 0)
            {
                var roleNames = request.Roles.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var roles = await _context.Roles.Where(r => roleNames.Contains(r.RoleName)).ToListAsync(cancellationToken);
                foreach (var role in roles)
                {
                    _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, IsActive = true, ValidFrom = DateTime.UtcNow });
                }
                await _context.SaveChangesAsync(cancellationToken);
            }

            var created = await _context.Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstAsync(u => u.Id == user.Id, cancellationToken);

            return CreatedAtAction(nameof(GetUser), new { userId = user.Id }, MapUser(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while creating user - Email: {Email}", request.Email);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    [HttpPut("{userId}")]
    
    public async Task<ActionResult> UpdateUser(
        long userId,
        [FromBody] UpdateUserAdminRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsUserAdmin())
            {
                return Forbid();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null)
            {
                return NotFound(new { error = "Utilisateur non trouvé" });
            }

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var email = request.Email.Trim();
                if (!email.Equals(user.Email, StringComparison.OrdinalIgnoreCase)
                    && await _context.Users.AnyAsync(u => u.Email == email && u.Id != userId, cancellationToken))
                {
                    return BadRequest(new { error = "Email existe déjà" });
                }
                user.Email = email;
            }

            if (!string.IsNullOrWhiteSpace(request.Username))
            {
                var username = request.Username.Trim();
                if (!username.Equals(user.Username, StringComparison.OrdinalIgnoreCase)
                    && await _context.Users.AnyAsync(u => u.Username == username && u.Id != userId, cancellationToken))
                {
                    return BadRequest(new { error = "Username existe déjà" });
                }
                user.Username = username;
            }

            if (request.FirstName != null) user.FirstName = request.FirstName.Trim();
            if (request.LastName != null) user.LastName = request.LastName.Trim();
            user.PhoneNumber = request.PhoneNumber?.Trim();
            user.Department = request.Department?.Trim();

            user.ChangedOn = DateTime.UtcNow;
            user.ChangedBy = GetActorName();

            if (request.IsActive.HasValue)
            {
                user.Status = request.IsActive.Value ? UserStatus.Active : UserStatus.Inactive;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while updating user {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    [HttpDelete("{userId}")]
    public async Task<ActionResult> DeleteUser(
        long userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsUserAdmin())
            {
                return Forbid();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null)
            {
                return NotFound(new { error = "Utilisateur non trouvé" });
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while deleting user {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    [HttpPost("{userId}/lock")]
    public async Task<ActionResult> LockUser(
        long userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsUserAdmin())
            {
                return Forbid();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null)
            {
                return NotFound(new { error = "Utilisateur non trouvé" });
            }

            user.LockedUntil = DateTime.UtcNow.AddMinutes(30);
            user.Status = UserStatus.Locked;
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true, lockedUntil = user.LockedUntil });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while locking user {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    [HttpPost("{userId}/unlock")]
    public async Task<ActionResult> UnlockUser(
        long userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsUserAdmin())
            {
                return Forbid();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null)
            {
                return NotFound(new { error = "Utilisateur non trouvé" });
            }

            user.LockedUntil = null;
            if (user.Status == UserStatus.Locked)
            {
                user.Status = UserStatus.Active;
            }
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while unlocking user {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    [HttpPost("{userId}/roles")]
    public async Task<ActionResult> AssignRoles(
        long userId,
        [FromBody] AssignRolesByNameRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsUserAdmin())
            {
                return Forbid();
            }

            var user = await _context.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null)
            {
                return NotFound(new { error = "Utilisateur non trouvé" });
            }

            var roleNames = (request.Roles ?? new List<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Remove existing roles (soft)
            var existing = await _context.UserRoles.Where(ur => ur.UserId == userId).ToListAsync(cancellationToken);
            _context.UserRoles.RemoveRange(existing);

            if (roleNames.Count > 0)
            {
                var roles = await _context.Roles.Where(r => roleNames.Contains(r.RoleName)).ToListAsync(cancellationToken);
                foreach (var role in roles)
                {
                    _context.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id, IsActive = true, ValidFrom = DateTime.UtcNow });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while assigning roles to user {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Une erreur interne s'est produite" });
        }
    }

    public sealed class UserListResponse
    {
        public List<UserListItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }

    public class UserListItemDto
    {
        public long UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string? PhoneNumber { get; set; }
        public List<string> Roles { get; set; } = new();
        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public sealed class UserDetailDto : UserListItemDto
    {
        public UserDetailDto() {}
        public UserDetailDto(UserListItemDto source)
        {
            UserId = source.UserId;
            Username = source.Username;
            Email = source.Email;
            FirstName = source.FirstName;
            LastName = source.LastName;
            Department = source.Department;
            PhoneNumber = source.PhoneNumber;
            Roles = source.Roles;
            IsActive = source.IsActive;
            IsLocked = source.IsLocked;
            LastLoginDate = source.LastLoginDate;
            CreatedDate = source.CreatedDate;
        }

        public string Status { get; set; } = string.Empty;
        public bool EmailVerified { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public DateTime? LockedUntil { get; set; }
    }

    public sealed class CreateUserAdminRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Department { get; set; }
        public List<string>? Roles { get; set; }
    }

    public sealed class UpdateUserAdminRequest
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Department { get; set; }
        public bool? IsActive { get; set; }
    }

    public sealed class AssignRolesByNameRequest
    {
        public List<string>? Roles { get; set; }
    }
}
