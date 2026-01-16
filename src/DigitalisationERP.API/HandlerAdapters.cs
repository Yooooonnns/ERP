using DigitalisationERP.Application.Identity.Commands.Handlers;
using IdentityPasswordHashingService = DigitalisationERP.Infrastructure.Identity.Services.PasswordHashingService;
using IdentityTokenService = DigitalisationERP.Infrastructure.Identity.Services.TokenService;
using DigitalisationERP.Infrastructure.Identity.Repositories;
using DigitalisationERP.Domain.Identity.Entities;

namespace DigitalisationERP.API;

/// <summary>
/// Adapter class for PasswordHashingService to handler interface.
/// </summary>
public class PasswordHashingServiceAdapter : IPasswordHashingServiceForHandlers
{
    private readonly IdentityPasswordHashingService _service;
    
    public PasswordHashingServiceAdapter(IdentityPasswordHashingService service)
    {
        _service = service;
    }
    
    public bool VerifyPassword(string password, string hash) => _service.VerifyPassword(password, hash);
}

/// <summary>
/// Adapter class for TokenService to handler interface.
/// </summary>
public class TokenServiceAdapter : ITokenServiceForHandlers
{
    private readonly IdentityTokenService _tokenService;
    
    public TokenServiceAdapter(IdentityTokenService tokenService)
    {
        _tokenService = tokenService;
    }
    
    public string GenerateAccessToken(User user) => _tokenService.GenerateAccessToken(user);
    public string GenerateRefreshToken() => _tokenService.GenerateRefreshToken();
}

/// <summary>
/// Adapter class for UserRepository to handler interface.
/// </summary>
public class UserRepositoryAdapter : IUserRepositoryForHandlers
{
    private readonly UserRepository _repo;
    
    public UserRepositoryAdapter(UserRepository repo)
    {
        _repo = repo;
    }
    
    public async Task<User?> GetByUsernameAsync(string username) => await _repo.GetByUsernameAsync(username);
}
