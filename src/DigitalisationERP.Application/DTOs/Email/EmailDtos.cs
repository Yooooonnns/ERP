namespace DigitalisationERP.Application.DTOs.Email;

/// <summary>
/// Email sending request
/// </summary>
public class SendEmailRequest
{
    public string ToEmail { get; set; } = string.Empty;
    public string? ToName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public List<string>? CcEmails { get; set; }
    public List<string>? BccEmails { get; set; }
    public int Priority { get; set; } = 2; // 1=High, 2=Normal, 3=Low
    public DateTime? ScheduledAt { get; set; }
    public List<EmailAttachment>? Attachments { get; set; }
}

/// <summary>
/// Email template request
/// </summary>
public class SendTemplateEmailRequest
{
    public string ToEmail { get; set; } = string.Empty;
    public string? ToName { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public Dictionary<string, object> TemplateData { get; set; } = new();
    public List<string>? CcEmails { get; set; }
    public int Priority { get; set; } = 2;
    public DateTime? ScheduledAt { get; set; }
}

/// <summary>
/// Email attachment
/// </summary>
public class EmailAttachment
{
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/octet-stream";
}

/// <summary>
/// Email verification request
/// </summary>
public class SendVerificationEmailRequest
{
    public long UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
}

/// <summary>
/// Password reset email request
/// </summary>
public class SendPasswordResetEmailRequest
{
    public string Email { get; set; } = string.Empty;
    public string ResetUrl { get; set; } = string.Empty;
}

/// <summary>
/// Production report email request
/// </summary>
public class SendProductionReportRequest
{
    public List<string> Recipients { get; set; } = new();
    public DateTime ReportDate { get; set; }
    public string ReportType { get; set; } = string.Empty; // Daily, Weekly, Monthly
    public byte[]? PdfReport { get; set; }
}

/// <summary>
/// Stock alert email request
/// </summary>
public class SendStockAlertRequest
{
    public List<string> Recipients { get; set; } = new();
    public string MaterialNumber { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal MinStockLevel { get; set; }
    public string AlertType { get; set; } = string.Empty; // Low, Critical, Out
}

/// <summary>
/// Maintenance alert email request
/// </summary>
public class SendMaintenanceAlertRequest
{
    public List<string> Recipients { get; set; } = new();
    public string MachineNumber { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty; // Scheduled, Critical, Breakdown
    public DateTime? ScheduledDate { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Email queue status response
/// </summary>
public class EmailQueueDto
{
    public long Id { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }
    public int SendAttempts { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedOn { get; set; }
}

/// <summary>
/// Account approval email request
/// </summary>
public class SendApprovalEmailRequest
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string Message { get; set; } = "Your account has been successfully validated. You can now log in to the application.";
}

/// <summary>
/// Account rejection email request
/// </summary>
public class SendRejectionEmailRequest
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string Reason { get; set; } = "Your account application did not meet our requirements. Please contact support for more information.";
}
