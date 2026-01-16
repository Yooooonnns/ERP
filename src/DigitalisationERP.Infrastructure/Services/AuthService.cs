using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using DigitalisationERP.Core.Entities.Auth;
using DigitalisationERP.Core.Entities.System;
using DigitalisationERP.Infrastructure.Data;
using DigitalisationERP.Application.DTOs.Auth;
using DigitalisationERP.Application.DTOs.Email;
using DigitalisationERP.Application.Interfaces;
using System.Security.Cryptography;

namespace DigitalisationERP.Infrastructure.Services;

public interface IAuthService
{
    Task<(bool Success, string Message, UserDto? User)> RegisterAsync(RegisterRequest request, string? verificationCallbackUrl = null);
    Task<(bool Success, string Message, LoginResponse? Response)> LoginAsync(LoginRequest request);
    Task<(bool Success, string Message, LoginResponse? Response)> RefreshTokenAsync(string refreshToken);
    Task<(bool Success, string Message)> ChangePasswordAsync(long userId, ChangePasswordRequest request);
    Task<bool> ValidateUserAsync(string username, string password);
    Task<User?> GetUserByIdAsync(long userId);
    Task<User?> GetUserByUsernameAsync(string username);
    
    // Email verification
    Task<(bool Success, string Message)> ResendVerificationEmailAsync(string email, string verificationCallbackUrl);
    Task<(bool Success, string Message)> VerifyEmailAsync(string token);
    
    // Password reset
    Task<(bool Success, string Message)> ForgotPasswordAsync(string email, string resetCallbackUrl);
    Task<(bool Success, string Message)> ResetPasswordAsync(string token, string newPassword);
    
    // S-User: Get pending registrations
    Task<List<UserDto>> GetPendingVerificationsAsync();
    
    // S-User/Admin: Create user accounts
    Task<(bool Success, string Message, CreateUserResponse? Response)> CreateUserAsync(CreateUserRequest request, long createdByUserId);
}

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IEmailService? _emailService;

    public AuthService(ApplicationDbContext context, ITokenService tokenService, IEmailService? emailService = null)
    {
        _context = context;
        _tokenService = tokenService;
        _emailService = emailService;
    }

    public async Task<(bool Success, string Message, UserDto? User)> RegisterAsync(RegisterRequest request, string? verificationCallbackUrl = null)
    {
        // Validate passwords match
        if (request.Password != request.ConfirmPassword)
        {
            return (false, "Passwords do not match", null);
        }

        // Check if username exists
        if (await _context.Set<User>().AnyAsync(u => u.Username == request.Username))
        {
            return (false, "Username already exists", null);
        }

        // Check if email exists
        if (await _context.Set<User>().AnyAsync(u => u.Email == request.Email))
        {
            return (false, "Email already exists", null);
        }

        // Create user
        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            EmailVerified = false, // Require email verification
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            EmployeeNumber = request.EmployeeNumber,
            Department = request.Department,
            PhoneNumber = request.PhoneNumber,
            Language = request.Language,
            UserType = UserType.Dialog, // Default to Dialog user
            Status = UserStatus.PendingActivation, // Pending until email verified
            ValidFrom = DateTime.UtcNow,
            PasswordLastChanged = DateTime.UtcNow,
            MustChangePassword = false
        };

        _context.Set<User>().Add(user);
        await _context.SaveChangesAsync();

        // Send verification email if callback URL provided
        if (!string.IsNullOrEmpty(verificationCallbackUrl) && _emailService != null)
        {
            try
            {
                await _emailService.SendVerificationEmailAsync(new SendVerificationEmailRequest
                {
                    UserId = user.Id,
                    Email = user.Email,
                    CallbackUrl = verificationCallbackUrl
                });
            }
            catch (Exception)
            {
                // Log error but don't fail registration
            }
        }

        var userDto = MapToUserDto(user, new List<string>());

        return (true, "User registered successfully. Please check your email to verify your account.", userDto);
    }

    public async Task<(bool Success, string Message, LoginResponse? Response)> LoginAsync(LoginRequest request)
    {
        try
        {
            var users = await _context.Set<User>()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .ToListAsync();

            // Allow login by username or email
            var user = users.FirstOrDefault(u => 
                u.Username == request.Username || 
                u.Email == request.Username);

        if (user == null)
        {
            // Log failed login attempt
            await LogActivityAsync(null, request.Username, ActivityType.FailedLogin, "Invalid username");
            return (false, "Invalid username or password", null);
        }

        // Check account status
        if (user.Status != UserStatus.Active)
        {
            return (false, $"Account is {user.Status}", null);
        }

        // Check if email is verified
        if (!user.EmailVerified)
        {
            return (false, "Please verify your email address before logging in", null);
        }

        // Check if account is locked
        if (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow)
        {
            var remainingMinutes = (user.LockedUntil.Value - DateTime.UtcNow).TotalMinutes;
            return (false, $"Account is locked. Try again in {Math.Ceiling(remainingMinutes)} minutes", null);
        }

        // Check if account is expired
        if (user.ValidTo.HasValue && user.ValidTo < DateTime.UtcNow)
        {
            return (false, "Account has expired", null);
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;

            // Lock account after 5 failed attempts
            if (user.FailedLoginAttempts >= 5)
            {
                user.LockedUntil = DateTime.UtcNow.AddMinutes(30);
                user.Status = UserStatus.Locked;
            }

            await _context.SaveChangesAsync();
            await LogActivityAsync(user.Id, user.Username, ActivityType.FailedLogin, "Invalid password");

            return (false, "Invalid username or password", null);
        }

        // Reset failed attempts on successful login
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLogin = DateTime.UtcNow;

        // Generate tokens
        var roles = user.UserRoles
            .Where(ur => ur.IsActive && 
                        (!ur.ValidTo.HasValue || ur.ValidTo >= DateTime.UtcNow))
            .Select(ur => ur.Role.RoleName)
            .ToList();

        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);

        await _context.SaveChangesAsync();
        await LogActivityAsync(user.Id, user.Username, ActivityType.Login, "Successful login");

        var response = new LoginResponse
        {
            Token = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddHours(8),
            User = MapToUserDto(user, roles)
        };

        return (true, "Login successful", response);
        }
        catch (Exception ex)
        {
            return (false, $"Login error: {ex.Message}", null);
        }
    }

    public async Task<(bool Success, string Message, LoginResponse? Response)> RefreshTokenAsync(string refreshToken)
    {
        var user = await _context.Set<User>()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);

        if (user == null || user.RefreshTokenExpiry < DateTime.UtcNow)
        {
            return (false, "Invalid or expired refresh token", null);
        }

        var roles = user.UserRoles
            .Where(ur => ur.IsActive)
            .Select(ur => ur.Role.RoleName)
            .ToList();

        var newAccessToken = _tokenService.GenerateAccessToken(user, roles);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);

        await _context.SaveChangesAsync();

        var response = new LoginResponse
        {
            Token = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddHours(8),
            User = MapToUserDto(user, roles)
        };

        return (true, "Token refreshed successfully", response);
    }

    public async Task<(bool Success, string Message)> ChangePasswordAsync(long userId, ChangePasswordRequest request)
    {
        var user = await _context.Set<User>().FindAsync(userId);
        if (user == null)
        {
            return (false, "User not found");
        }

        // Verify current password
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return (false, "Current password is incorrect");
        }

        // Validate new passwords match
        if (request.NewPassword != request.ConfirmNewPassword)
        {
            return (false, "New passwords do not match");
        }

        // Update password
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordLastChanged = DateTime.UtcNow;
        user.MustChangePassword = false;

        await _context.SaveChangesAsync();
        await LogActivityAsync(userId, user.Username, ActivityType.PasswordChange, "Password changed successfully");

        return (true, "Password changed successfully");
    }

    public async Task<bool> ValidateUserAsync(string username, string password)
    {
        var allUsers = await _context.Set<User>().ToListAsync();
        var user = allUsers.FirstOrDefault(u => u.Username == username && u.Status == UserStatus.Active);

        if (user == null) return false;

        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
    }

    public async Task<User?> GetUserByIdAsync(long userId)
    {
        return await _context.Set<User>()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        var allUsers = await _context.Set<User>()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .ToListAsync();
        return allUsers.FirstOrDefault(u => u.Username == username);
    }

    private UserDto MapToUserDto(User user, List<string> roles)
    {
        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            UserType = user.UserType.ToString(),
            Status = user.Status.ToString(),
            EmployeeNumber = user.EmployeeNumber,
            Department = user.Department,
            PhoneNumber = user.PhoneNumber,
            Language = user.Language,
            LastLogin = user.LastLogin,
            Roles = roles
        };
    }

    private async Task LogActivityAsync(long? userId, string username, ActivityType activityType, string description)
    {
        var activity = new UserActivity
        {
            UserId = userId ?? 0,
            Username = username,
            ActivityType = activityType,
            Description = description,
            Timestamp = DateTime.UtcNow,
            IsSuccessful = activityType != ActivityType.FailedLogin
        };

        _context.Set<UserActivity>().Add(activity);
        await _context.SaveChangesAsync();
    }

    public async Task<(bool Success, string Message)> ResendVerificationEmailAsync(string email, string verificationCallbackUrl)
    {
        var user = await _context.Set<User>().FirstOrDefaultAsync(u => u.Email == email);
        
        if (user == null)
        {
            return (false, "User not found");
        }

        if (user.EmailVerified)
        {
            return (false, "Email is already verified");
        }

        if (_emailService == null)
        {
            return (false, "Email service is not configured");
        }

        try
        {
            await _emailService.SendVerificationEmailAsync(new SendVerificationEmailRequest
            {
                UserId = user.Id,
                Email = user.Email,
                CallbackUrl = verificationCallbackUrl
            });

            return (true, "Verification email sent successfully");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to send email: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> VerifyEmailAsync(string token)
    {
        var verificationToken = await _context.EmailVerificationTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token && !t.IsUsed && t.TokenType == "EmailVerification");

        if (verificationToken == null)
        {
            return (false, "Invalid verification token");
        }

        if (verificationToken.ExpiresAt < DateTime.UtcNow)
        {
            return (false, "Verification token has expired");
        }

        var user = verificationToken.User;
        user.EmailVerified = true;
        user.EmailVerifiedAt = DateTime.UtcNow;
        user.Status = UserStatus.Active;

        verificationToken.IsUsed = true;
        verificationToken.UsedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await LogActivityAsync(user.Id, user.Username, ActivityType.EmailVerified, "Email verified successfully");

        // Send welcome email
        if (_emailService != null)
        {
            try
            {
                await _emailService.SendWelcomeEmailAsync(user.Email, user.Username, $"{user.FirstName} {user.LastName}");
            }
            catch { }
        }

        return (true, "Email verified successfully");
    }

    public async Task<(bool Success, string Message)> ForgotPasswordAsync(string email, string resetCallbackUrl)
    {
        var user = await _context.Set<User>().FirstOrDefaultAsync(u => u.Email == email);
        
        if (user == null)
        {
            // Return success anyway to prevent email enumeration
            return (true, "If the email exists, a password reset link has been sent");
        }

        if (_emailService == null)
        {
            return (false, "Email service is not configured");
        }

        // Generate reset token
        var resetToken = Guid.NewGuid().ToString("N");
        var tokenEntity = new EmailVerificationToken
        {
            UserId = user.Id,
            Token = resetToken,
            Email = user.Email,
            ExpiresAt = DateTime.UtcNow.AddHours(1), // 1 hour expiry
            IsUsed = false,
            TokenType = "PasswordReset",
            ClientId = "001"
        };

        _context.EmailVerificationTokens.Add(tokenEntity);
        await _context.SaveChangesAsync();

        // Send password reset email
        try
        {
            var resetUrl = $"{resetCallbackUrl}?token={resetToken}";
            await _emailService.SendPasswordResetEmailAsync(new SendPasswordResetEmailRequest
            {
                Email = user.Email,
                ResetUrl = resetUrl
            });

            await LogActivityAsync(user.Id, user.Username, ActivityType.PasswordResetRequested, "Password reset requested");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to send reset email: {ex.Message}");
        }

        return (true, "If the email exists, a password reset link has been sent");
    }

    public async Task<(bool Success, string Message)> ResetPasswordAsync(string token, string newPassword)
    {
        var resetToken = await _context.EmailVerificationTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token && !t.IsUsed && t.TokenType == "PasswordReset");

        if (resetToken == null)
        {
            return (false, "Invalid or expired reset token");
        }

        if (resetToken.ExpiresAt < DateTime.UtcNow)
        {
            return (false, "Reset token has expired");
        }

        var user = resetToken.User;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.PasswordLastChanged = DateTime.UtcNow;
        user.MustChangePassword = false;

        // Reset any account lockout
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        if (user.Status == UserStatus.Locked)
        {
            user.Status = UserStatus.Active;
        }

        resetToken.IsUsed = true;
        resetToken.UsedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await LogActivityAsync(user.Id, user.Username, ActivityType.PasswordChange, "Password reset successfully");

        // Send notification email
        if (_emailService != null)
        {
            try
            {
                await _emailService.SendPasswordChangedNotificationAsync(user.Email, user.Username);
            }
            catch { }
        }

        return (true, "Password reset successfully");
    }

    public async Task<(bool Success, string Message, CreateUserResponse? Response)> CreateUserAsync(CreateUserRequest request, long createdByUserId)
    {
        // Check if username exists
        var allUsers = await _context.Set<User>().ToListAsync();
        if (allUsers.Any(u => u.Username == request.Username))
        {
            return (false, "Username already exists", null);
        }

        // Check if email exists
        if (allUsers.Any(u => u.Email == request.Email))
        {
            return (false, "Email already exists", null);
        }

        // Generate temporary password
        var tempPassword = GenerateTemporaryPassword();

        // Parse user type
        if (!Enum.TryParse<UserType>(request.UserType, true, out var userType))
        {
            userType = UserType.Dialog;
        }

        // Create user
        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            EmailVerified = false,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword),
            FirstName = request.FirstName,
            LastName = request.LastName,
            EmployeeNumber = request.EmployeeNumber,
            Department = request.Department,
            PhoneNumber = request.PhoneNumber,
            UserType = userType,
            Status = UserStatus.Active,
            ValidFrom = DateTime.UtcNow,
            PasswordLastChanged = DateTime.UtcNow,
            MustChangePassword = request.MustChangePassword,
            Language = "EN",
            TimeZone = "UTC"
        };

        _context.Set<User>().Add(user);
        await _context.SaveChangesAsync();

        // Assign roles
        if (request.RoleNames.Any())
        {
            var roles = await _context.Set<Role>()
                .Where(r => request.RoleNames.Contains(r.RoleName))
                .ToListAsync();

            foreach (var role in roles)
            {
                var userRole = new UserRole
                {
                    UserId = user.Id,
                    RoleId = role.Id,
                    IsActive = true,
                    ValidFrom = DateTime.UtcNow,
                    ClientId = "001"
                };
                _context.Set<UserRole>().Add(userRole);
            }

            await _context.SaveChangesAsync();
        }

        await LogActivityAsync(user.Id, user.Username, ActivityType.UserCreated, $"User created by admin (ID: {createdByUserId})");

        bool emailSent = false;

        // Send credentials email
        if (request.SendCredentialsEmail && _emailService != null)
        {
            try
            {
                var emailBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #2196F3; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; background: #f9f9f9; }}
        .credentials {{ background: #fff; border: 1px solid #ddd; padding: 15px; margin: 20px 0; }}
        .credential-label {{ font-weight: bold; color: #666; }}
        .credential-value {{ color: #2196F3; font-family: monospace; font-size: 16px; }}
        .warning {{ background: #fff3cd; border-left: 4px solid #ffc107; padding: 10px; margin: 20px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🏭 DigitalisationERP Account Created</h1>
        </div>
        <div class='content'>
            <h2>Hello {user.FirstName} {user.LastName},</h2>
            <p>An account has been created for you in the DigitalisationERP system.</p>
            
            <div class='credentials'>
                <p><span class='credential-label'>Username:</span> <span class='credential-value'>{user.Username}</span></p>
                <p><span class='credential-label'>Temporary Password:</span> <span class='credential-value'>{tempPassword}</span></p>
                <p><span class='credential-label'>Email:</span> <span class='credential-value'>{user.Email}</span></p>
            </div>

            {(request.MustChangePassword ? "<div class='warning'><strong>⚠️ Important:</strong> You must change your password on first login.</div>" : "")}
            
            <h3>Your Assigned Roles:</h3>
            <ul>
                {string.Join("", request.RoleNames.Select(r => $"<li>{r}</li>"))}
            </ul>

            <p>Please log in to the system using the credentials above and familiarize yourself with your assigned permissions.</p>
            
            <p>If you have any questions, please contact your system administrator.</p>
        </div>
        <div class='footer'>
            <p>© 2025 DigitalisationERP. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

                await _emailService.SendEmailAsync(new SendEmailRequest
                {
                    ToEmail = user.Email,
                    ToName = $"{user.FirstName} {user.LastName}",
                    Subject = "Your DigitalisationERP Account Credentials",
                    Body = emailBody,
                    Priority = 1
                });

                emailSent = true;
            }
            catch { }
        }

        var response = new CreateUserResponse
        {
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            TemporaryPassword = tempPassword, // Return in response for manual delivery if email fails
            EmailSent = emailSent,
            Message = emailSent 
                ? "User created successfully. Credentials sent via email." 
                : "User created successfully. Please provide the temporary password to the user manually."
        };

        return (true, response.Message, response);
    }

    private string GenerateTemporaryPassword()
    {
        const string upperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lowerChars = "abcdefghijklmnopqrstuvwxyz";
        const string digitChars = "0123456789";
        const string specialChars = "!@#$%^&*";
        
        var password = new char[12];
        password[0] = upperChars[RandomNumberGenerator.GetInt32(upperChars.Length)];
        password[1] = lowerChars[RandomNumberGenerator.GetInt32(lowerChars.Length)];
        password[2] = digitChars[RandomNumberGenerator.GetInt32(digitChars.Length)];
        password[3] = specialChars[RandomNumberGenerator.GetInt32(specialChars.Length)];

        var allChars = upperChars + lowerChars + digitChars + specialChars;
        for (int i = 4; i < 12; i++)
        {
            password[i] = allChars[RandomNumberGenerator.GetInt32(allChars.Length)];
        }

        // Shuffle
        for (int i = password.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (password[i], password[j]) = (password[j], password[i]);
        }

        return new string(password);
    }

    public async Task<List<UserDto>> GetPendingVerificationsAsync()
    {
        var pendingUsers = await _context.Set<User>()
            .Where(u => u.Status == UserStatus.PendingActivation)
            .ToListAsync();

        return pendingUsers.Select(u => new UserDto
        {
            Id = u.Id,
            Username = u.Username,
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Status = u.Status.ToString(),
            Department = u.Department,
            PhoneNumber = u.PhoneNumber,
            EmployeeNumber = u.EmployeeNumber
        }).ToList();
    }}