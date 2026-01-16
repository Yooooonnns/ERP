namespace DigitalisationERP.Application.DTOs.Identity.Responses;

public class RegisterResponse
{
    public long UserId { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Message { get; set; } = null!;
}
