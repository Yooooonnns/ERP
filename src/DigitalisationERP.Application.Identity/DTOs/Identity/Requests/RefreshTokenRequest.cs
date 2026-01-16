namespace DigitalisationERP.Application.DTOs.Identity.Requests;

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = null!;
    public string AccessToken { get; set; } = null!;
}
