namespace DigitalisationERP.Application.DTOs.Auth;

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? Department { get; set; }
    public string? PhoneNumber { get; set; }
    public string Language { get; set; } = "EN";
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = null!;
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public class UserDto
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public string UserType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? Department { get; set; }
    public string? PhoneNumber { get; set; }
    public string Language { get; set; } = "EN";
    public DateTime? LastLogin { get; set; }
    public List<string> Roles { get; set; } = new();
}

public class AssignRoleRequest
{
    public long UserId { get; set; }
    public long RoleId { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
}

public class RoleDto
{
    public long Id { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RoleType { get; set; } = string.Empty;
    public string? Module { get; set; }
    public bool IsActive { get; set; }
    public bool IsSystemRole { get; set; }
}

/// <summary>
/// Resend verification email request
/// </summary>
public class ResendVerificationRequest
{
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Verify email request
/// </summary>
public class VerifyEmailRequest
{
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// Forgot password request
/// </summary>
public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Reset password with token request
/// </summary>
public class ResetPasswordWithTokenRequest
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>
/// Create user request (S-User/Admin only)
/// </summary>
public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? Department { get; set; }
    public string? PhoneNumber { get; set; }
    public string UserType { get; set; } = "Dialog";
    public List<string> RoleNames { get; set; } = new();
    public bool SendCredentialsEmail { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;
}

/// <summary>
/// Create user response
/// </summary>
public class CreateUserResponse
{
    public long UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? TemporaryPassword { get; set; }
    public bool EmailSent { get; set; }
    public string Message { get; set; } = string.Empty;
}
