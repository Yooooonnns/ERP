namespace DigitalisationERP.Application.DTOs.InternalEmail;

public class SendInternalEmailRequest
{
    public List<int> RecipientIds { get; set; } = new();
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public List<InternalEmailAttachmentDto>? Attachments { get; set; }
}

public class InternalEmailDto
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderInitials { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public string Time { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public List<InternalEmailRecipientDto> Recipients { get; set; } = new();
    public List<InternalEmailAttachmentDto> Attachments { get; set; } = new();
    public bool HasAttachment => Attachments?.Any() ?? false;
}

public class InternalEmailRecipientDto
{
    public int RecipientId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // To, Cc, Bcc
}

public class InternalEmailAttachmentDto
{
    public int? Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
}

public class WorkerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
}

public class InternalEmailListResponse
{
    public List<InternalEmailDto> Emails { get; set; } = new();
    public int TotalCount { get; set; }
    public int UnreadCount { get; set; }
}

public class InternalThreadDto
{
    public int OtherUserId { get; set; }
    public string OtherUserName { get; set; } = string.Empty;
    public string OtherUserEmail { get; set; } = string.Empty;
    public string OtherUserInitials { get; set; } = string.Empty;
    public string OtherUserRole { get; set; } = string.Empty;
    public string OtherUserDepartment { get; set; } = string.Empty;

    public int LastEmailId { get; set; }
    public string LastSubject { get; set; } = string.Empty;
    public string LastPreview { get; set; } = string.Empty;
    public DateTime LastSentAt { get; set; }
    public string LastTime { get; set; } = string.Empty;
    public int UnreadCount { get; set; }
}

public class InternalThreadMessageDto
{
    public int EmailId { get; set; }
    public int SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderInitials { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty; // "in" or "out"
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public string Time { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public List<InternalEmailAttachmentDto> Attachments { get; set; } = new();
}
