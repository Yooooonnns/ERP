namespace DigitalisationERP.Application.DTOs.Identity;

public class AuthorizationObjectDto
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string ModuleName { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
}
