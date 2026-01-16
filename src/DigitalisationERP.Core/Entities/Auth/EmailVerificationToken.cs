namespace DigitalisationERP.Core.Entities.Auth;

/// <summary>
/// Email verification tokens for user email confirmation
/// </summary>
public class EmailVerificationToken : BaseEntity
{
    /// <summary>
    /// User ID
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Verification token (GUID)
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Email to verify
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Token expiry date/time
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Is token used?
    /// </summary>
    public bool IsUsed { get; set; }

    /// <summary>
    /// When was the token used?
    /// </summary>
    public DateTime? UsedAt { get; set; }

    /// <summary>
    /// Token type (EmailVerification, PasswordReset, EmailChange)
    /// </summary>
    public string TokenType { get; set; } = string.Empty;

    // Navigation property
    public User User { get; set; } = null!;
}
