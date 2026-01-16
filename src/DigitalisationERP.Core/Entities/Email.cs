using DigitalisationERP.Core.Entities.Auth;

namespace DigitalisationERP.Core.Entities;

public class Email
{
    public int Id { get; set; }
    public long SenderId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsDraft { get; set; }
    public DateTime? ReadAt { get; set; }
    
    // Navigation properties
    public User Sender { get; set; } = null!;
    public ICollection<EmailRecipient> Recipients { get; set; } = new List<EmailRecipient>();
    public ICollection<EmailAttachment> Attachments { get; set; } = new List<EmailAttachment>();
}

public class EmailRecipient
{
    public int Id { get; set; }
    public int EmailId { get; set; }
    public long RecipientId { get; set; }
    public RecipientType Type { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    
    // Navigation properties
    public Email Email { get; set; } = null!;
    public User Recipient { get; set; } = null!;
}

public enum RecipientType
{
    To,
    Cc,
    Bcc
}

public class EmailAttachment
{
    public int Id { get; set; }
    public int EmailId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
    
    // Navigation properties
    public Email Email { get; set; } = null!;
}
