namespace DigitalisationERP.Application.DTOs.Identity.Responses;

public class LoginResponse
{
    public long UserId { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string AccessToken { get; set; } = null!;
    public string? RefreshToken { get; set; }
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public DateTime ExpiresAt { get; set; }
    public List<string> Roles { get; set; } = new();
    
    // Convenience property for User DTO (for compatibility)
    public UserDto? User { get; set; }
}
