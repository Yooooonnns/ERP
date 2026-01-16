namespace DigitalisationERP.Application.DTOs.Identity.Requests;

public class AuthObjectCreateRequest
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string ModuleName { get; set; } = null!;
}
