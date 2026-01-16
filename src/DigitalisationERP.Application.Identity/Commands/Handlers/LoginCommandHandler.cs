using DigitalisationERP.Core;
using DigitalisationERP.Application.DTOs.Identity.Responses;
using DigitalisationERP.Domain.Identity.Entities;
using MediatR;

namespace DigitalisationERP.Application.Identity.Commands.Handlers;

/// <summary>
/// Interfaces for handler dependencies - will be resolved from Infrastructure.Identity
/// </summary>

public interface IPasswordHashingServiceForHandlers
{
    bool VerifyPassword(string password, string hash);
}

public interface ITokenServiceForHandlers
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
}

public interface IUserRepositoryForHandlers
{
    Task<User?> GetByUsernameAsync(string username);
}

/// <summary>
/// Handler for the LoginCommand - authenticates user and returns JWT tokens.
/// </summary>
public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IPasswordHashingServiceForHandlers _passwordHashingService;
    private readonly ITokenServiceForHandlers _tokenService;
    private readonly IUserRepositoryForHandlers _userRepository;

    public LoginCommandHandler(
        IPasswordHashingServiceForHandlers passwordHashingService,
        ITokenServiceForHandlers tokenService,
        IUserRepositoryForHandlers userRepository)
    {
        _passwordHashingService = passwordHashingService;
        _tokenService = tokenService;
        _userRepository = userRepository;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Get user by username
            var user = await _userRepository.GetByUsernameAsync(request.Username);
            
            if (user == null)
                return Result<LoginResponse>.Fail("Invalid username or password");

            // Verify password
            if (!_passwordHashingService.VerifyPassword(request.Password, user.Password.Hash))
                return Result<LoginResponse>.Fail("Invalid username or password");

            // Check if user is locked
            if (user.IsLocked)
                return Result<LoginResponse>.Fail("User account is locked");

            // Generate tokens
            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();
            var expiresAt = DateTime.UtcNow.AddHours(1);

            var response = new LoginResponse
            {
                UserId = user.UserId,
                Username = user.Username.Value,
                Email = user.Email.Value,
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt,
                ExpiresIn = 3600, // 1 hour in seconds
                Roles = new List<string>() // TODO: Load from user roles
            };

            return Result<LoginResponse>.Ok(response);
        }
        catch (Exception ex)
        {
            return Result<LoginResponse>.Fail($"Login failed: {ex.Message}");
        }
    }
}
