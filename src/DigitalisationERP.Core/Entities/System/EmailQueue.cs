namespace DigitalisationERP.Core.Entities.System;

/// <summary>
/// Email queue for asynchronous email sending
/// </summary>
public class EmailQueue : BaseEntity
{
    /// <summary>
    /// Recipient email address
    /// </summary>
    public string ToEmail { get; set; } = string.Empty;

    /// <summary>
    /// Recipient name
    /// </summary>
    public string? ToName { get; set; }

    /// <summary>
    /// CC email addresses (comma-separated)
    /// </summary>
    public string? CcEmails { get; set; }

    /// <summary>
    /// BCC email addresses (comma-separated)
    /// </summary>
    public string? BccEmails { get; set; }

    /// <summary>
    /// Email subject
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Email body (HTML)
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Email template name (optional)
    /// </summary>
    public string? TemplateName { get; set; }

    /// <summary>
    /// Template data (JSON)
    /// </summary>
    public string? TemplateData { get; set; }

    /// <summary>
    /// Email priority (1=High, 2=Normal, 3=Low)
    /// </summary>
    public int Priority { get; set; } = 2;

    /// <summary>
    /// Email status
    /// </summary>
    public EmailStatus Status { get; set; }

    /// <summary>
    /// Scheduled send time (null = send immediately)
    /// </summary>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>
    /// When was email sent?
    /// </summary>
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// Send attempts count
    /// </summary>
    public int SendAttempts { get; set; }

    /// <summary>
    /// Maximum send attempts
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Related user ID (optional)
    /// </summary>
    public long? UserId { get; set; }

    /// <summary>
    /// Related entity type (ProductionOrder, Material, etc.)
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Related entity ID
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// Has attachments?
    /// </summary>
    public bool HasAttachments { get; set; }

    /// <summary>
    /// Attachment file paths (JSON array)
    /// </summary>
    public string? AttachmentPaths { get; set; }
}

/// <summary>
/// Email status enum
/// </summary>
public enum EmailStatus
{
    Pending = 1,
    Sending = 2,
    Sent = 3,
    Failed = 4,
    Cancelled = 5
}
