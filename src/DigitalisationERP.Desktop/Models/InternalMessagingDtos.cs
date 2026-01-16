using System.Collections.Generic;

namespace DigitalisationERP.Desktop.Models.InternalMessaging;

public sealed class WorkerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
}

public sealed class InternalEmailAttachmentDto
{
    public int? Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
}

public sealed class SendInternalEmailRequest
{
    public List<int> RecipientIds { get; set; } = new();
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public List<InternalEmailAttachmentDto>? Attachments { get; set; }
}
