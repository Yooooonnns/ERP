using System;
using System.Collections.Generic;

namespace DigitalisationERP.Core.Entities;

public class ProductionChatMessage
{
    public int Id { get; set; }
    public string MessageText { get; set; } = string.Empty;
    
    // Sender
    public string SenderUserId { get; set; } = string.Empty;
    public string SenderUserName { get; set; } = string.Empty;
    
    // Context (optional - can be general chat or related to specific entity)
    public long? ProductionPostId { get; set; }
    public ProductionPost? ProductionPost { get; set; }
    
    public long? ProductionTaskId { get; set; }
    public ProductionTask? ProductionTask { get; set; }
    
    public int? IssueEscalationId { get; set; }
    public IssueEscalation? IssueEscalation { get; set; }
    
    // Message type
    public ChatMessageTypeEnum MessageType { get; set; }
    
    // Attachments
    public string? AttachmentUrls { get; set; } // JSON array of file URLs
    
    // Status
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    
    // Thread
    public int? ParentMessageId { get; set; }
    public ProductionChatMessage? ParentMessage { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public ICollection<ProductionChatMessage> Replies { get; set; } = new List<ProductionChatMessage>();
}

public enum ChatMessageTypeEnum
{
    General,
    Alert,
    Handover,
    Issue,
    Maintenance,
    Quality,
    Material,
    System
}
