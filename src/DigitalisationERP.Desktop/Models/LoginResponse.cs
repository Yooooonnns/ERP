using DigitalisationERP.Application.DTOs.Identity;
using DigitalisationERP.Application.DTOs.Identity.Responses;
using System.Text.Json.Serialization;

namespace DigitalisationERP.Desktop.Models;

public class LoginResponse
{
    [JsonPropertyName("token")]
    public string? AccessToken { get; set; }
    
    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }
    
    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }
    
    [JsonPropertyName("user")]
    public UserDto? User { get; set; }

    /// <summary>
    /// Récupère le rôle de l'utilisateur
    /// </summary>
    public string? Role => User?.RoleCodes?.FirstOrDefault();

    /// <summary>
    /// Récupère l'ID de l'utilisateur
    /// </summary>
    public string? UserId => User?.Email; // Utiliser Email si Id n'existe pas
}
