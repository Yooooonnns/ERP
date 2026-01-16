using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigitalisationERP.Infrastructure.Data;
using DigitalisationERP.Application.DTOs.InternalEmail;
using DigitalisationERP.Core.Entities;
using System.Security.Claims;
using Microsoft.AspNetCore.StaticFiles;

namespace DigitalisationERP.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class InternalEmailController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public InternalEmailController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all workers/users grouped by department
    /// </summary>
    [HttpGet("workers")]
    public async Task<ActionResult<List<WorkerDto>>> GetWorkers(
        [FromQuery] string? department = null,
        [FromQuery] string? role = null,
        [FromQuery] string? q = null)
    {
        // Return all users with email for now (no verification filter)
        var query = _context.Users
            .Where(u => u.Email != null);

        if (!string.IsNullOrEmpty(department))
        {
            query = query.Where(u => u.Department != null && u.Department.Contains(department));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u => u.UserRoles.Any(ur => ur.Role.RoleName == role));
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(u =>
                (u.Username != null && u.Username.Contains(q)) ||
                (u.Email != null && u.Email.Contains(q)) ||
                (u.FirstName != null && u.FirstName.Contains(q)) ||
                (u.LastName != null && u.LastName.Contains(q)));
        }

        var workers = await query
            .Select(u => new WorkerDto
            {
                Id = (int)u.Id,
                Name = u.Username,
                Email = u.Email!,
                Role = u.UserRoles.Select(ur => ur.Role.RoleName).FirstOrDefault() ?? "User",
                Department = u.Department ?? "General",
                Initials = GetInitials(u.Username)
            })
            .ToListAsync();

        return Ok(workers);
    }

    /// <summary>
    /// Send an internal email
    /// </summary>
    [HttpPost("send")]
    public async Task<ActionResult<InternalEmailDto>> SendEmail([FromBody] SendInternalEmailRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var email = new DigitalisationERP.Core.Entities.Email
        {
            SenderId = userId,
            Subject = request.Subject,
            Body = request.Body,
            SentAt = DateTime.UtcNow,
            IsDraft = false
        };

        _context.Emails.Add(email);
        await _context.SaveChangesAsync();

        // Add recipients
        foreach (var recipientId in request.RecipientIds)
        {
            var recipient = new EmailRecipient
            {
                EmailId = email.Id,
                RecipientId = recipientId,
                Type = RecipientType.To,
                IsRead = false
            };
            _context.EmailRecipients.Add(recipient);
        }

        // Add attachments
        if (request.Attachments != null)
        {
            foreach (var att in request.Attachments)
            {
                var attachment = new EmailAttachment
                {
                    EmailId = email.Id,
                    FileName = att.FileName,
                    FilePath = att.FilePath,
                    FileSize = att.FileSize,
                    UploadedAt = DateTime.UtcNow
                };
                _context.EmailAttachments.Add(attachment);
            }
        }

        await _context.SaveChangesAsync();

        return Ok(await GetEmailDto(email.Id, userId));
    }

    /// <summary>
    /// Upload an attachment file for internal email.
    /// Returns attachment metadata to be used in SendInternalEmailRequest.Attachments.
    /// </summary>
    [HttpPost("attachments/upload")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<InternalEmailAttachmentDto>> UploadAttachment([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded" });

        var folderRelative = Path.Combine("App_Data", "email_attachments");
        var folderAbsolute = Path.Combine(AppContext.BaseDirectory, folderRelative);
        Directory.CreateDirectory(folderAbsolute);

        var safeFileName = Path.GetFileName(file.FileName);
        var storedFileName = $"{Guid.NewGuid():N}_{safeFileName}";
        var relativePath = Path.Combine(folderRelative, storedFileName);
        var absolutePath = Path.Combine(folderAbsolute, storedFileName);

        await using (var stream = System.IO.File.Create(absolutePath))
        {
            await file.CopyToAsync(stream);
        }

        return Ok(new InternalEmailAttachmentDto
        {
            FileName = safeFileName,
            FilePath = relativePath.Replace('\\', '/'),
            FileSize = file.Length
        });
    }

    /// <summary>
    /// Download an attachment by ID (must be sender or recipient).
    /// </summary>
    [HttpGet("attachments/{attachmentId:int}/download")]
    public async Task<IActionResult> DownloadAttachment(int attachmentId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var attachment = await _context.EmailAttachments
            .Include(a => a.Email)
                .ThenInclude(e => e.Recipients)
            .Include(a => a.Email)
                .ThenInclude(e => e.Sender)
            .FirstOrDefaultAsync(a => a.Id == attachmentId);

        if (attachment == null)
            return NotFound();

        var email = attachment.Email;
        var isAllowed = email.SenderId == userId || email.Recipients.Any(r => r.RecipientId == userId);
        if (!isAllowed)
            return Forbid();

        var relative = (attachment.FilePath ?? string.Empty).TrimStart('/', '\\');
        if (string.IsNullOrWhiteSpace(relative) || relative.Contains(".."))
            return BadRequest(new { message = "Invalid attachment path" });

        var absolute = Path.Combine(AppContext.BaseDirectory, relative.Replace('/', '\\'));
        if (!System.IO.File.Exists(absolute))
            return NotFound();

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(attachment.FileName, out var contentType))
            contentType = "application/octet-stream";

        var bytes = await System.IO.File.ReadAllBytesAsync(absolute);
        return File(bytes, contentType, attachment.FileName);
    }

    /// <summary>
    /// Get conversation threads for current user (one-to-one threads).
    /// </summary>
    [HttpGet("threads")]
    public async Task<ActionResult<List<InternalThreadDto>>> GetThreads([FromQuery] int take = 200)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Load recent emails involving the current user.
        var emails = await _context.Emails
            .Include(e => e.Sender).ThenInclude(s => s.UserRoles).ThenInclude(ur => ur.Role)
            .Include(e => e.Recipients).ThenInclude(r => r.Recipient).ThenInclude(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(e => !e.IsDraft && (e.SenderId == userId || e.Recipients.Any(r => r.RecipientId == userId)))
            .OrderByDescending(e => e.SentAt)
            .Take(Math.Clamp(take, 1, 1000))
            .ToListAsync();

        var threads = new Dictionary<int, InternalThreadDto>();

        foreach (var email in emails)
        {
            if (email.SenderId == userId)
            {
                foreach (var r in email.Recipients)
                {
                    var other = r.Recipient;
                    var otherId = (int)r.RecipientId;
                    if (otherId == userId) continue;

                    if (!threads.TryGetValue(otherId, out var thread))
                    {
                        thread = new InternalThreadDto
                        {
                            OtherUserId = otherId,
                            OtherUserName = other.Username,
                            OtherUserEmail = other.Email ?? "",
                            OtherUserInitials = GetInitials(other.Username),
                            OtherUserRole = other.UserRoles.Select(ur => ur.Role.RoleName).FirstOrDefault() ?? "User",
                            OtherUserDepartment = other.Department ?? "General",
                            UnreadCount = 0
                        };
                        threads[otherId] = thread;
                    }

                    // Update last message if newer
                    if (email.SentAt >= thread.LastSentAt)
                    {
                        thread.LastEmailId = email.Id;
                        thread.LastSubject = email.Subject;
                        thread.LastPreview = email.Body.Length > 100 ? email.Body.Substring(0, 100) + "..." : email.Body;
                        thread.LastSentAt = email.SentAt;
                        thread.LastTime = GetTimeAgo(email.SentAt);
                    }
                }
            }
            else
            {
                // Incoming from another user to me
                var other = email.Sender;
                var otherId = (int)email.SenderId;

                if (!threads.TryGetValue(otherId, out var thread))
                {
                    thread = new InternalThreadDto
                    {
                        OtherUserId = otherId,
                        OtherUserName = other.Username,
                        OtherUserEmail = other.Email ?? "",
                        OtherUserInitials = GetInitials(other.Username),
                        OtherUserRole = other.UserRoles.Select(ur => ur.Role.RoleName).FirstOrDefault() ?? "User",
                        OtherUserDepartment = other.Department ?? "General",
                        UnreadCount = 0
                    };
                    threads[otherId] = thread;
                }

                var recipientRow = email.Recipients.FirstOrDefault(r => r.RecipientId == userId);
                if (recipientRow != null && !recipientRow.IsRead)
                    thread.UnreadCount += 1;

                if (email.SentAt >= thread.LastSentAt)
                {
                    thread.LastEmailId = email.Id;
                    thread.LastSubject = email.Subject;
                    thread.LastPreview = email.Body.Length > 100 ? email.Body.Substring(0, 100) + "..." : email.Body;
                    thread.LastSentAt = email.SentAt;
                    thread.LastTime = GetTimeAgo(email.SentAt);
                }
            }
        }

        var ordered = threads.Values
            .OrderByDescending(t => t.LastSentAt)
            .ToList();

        return Ok(ordered);
    }

    /// <summary>
    /// Get messages between current user and another user. Marks incoming messages as read.
    /// </summary>
    [HttpGet("threads/{otherUserId:int}")]
    public async Task<ActionResult<List<InternalThreadMessageDto>>> GetThreadMessages(int otherUserId, [FromQuery] int take = 200)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var emails = await _context.Emails
            .Include(e => e.Sender)
            .Include(e => e.Recipients).ThenInclude(r => r.Recipient)
            .Include(e => e.Attachments)
            .Where(e => !e.IsDraft &&
                        ((e.SenderId == userId && e.Recipients.Any(r => r.RecipientId == otherUserId)) ||
                         (e.SenderId == otherUserId && e.Recipients.Any(r => r.RecipientId == userId))))
            .OrderByDescending(e => e.SentAt)
            .Take(Math.Clamp(take, 1, 2000))
            .ToListAsync();

        // Mark incoming as read
        var incomingRecipientRows = await _context.EmailRecipients
            .Where(r => r.RecipientId == userId && !r.IsRead && r.Email.SenderId == otherUserId)
            .ToListAsync();

        if (incomingRecipientRows.Count > 0)
        {
            foreach (var r in incomingRecipientRows)
            {
                r.IsRead = true;
                r.ReadAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        var result = emails
            .OrderBy(e => e.SentAt)
            .Select(e =>
            {
                var recipientRow = e.Recipients.FirstOrDefault(r => r.RecipientId == userId);
                var isIncoming = e.SenderId == otherUserId;
                return new InternalThreadMessageDto
                {
                    EmailId = e.Id,
                    SenderId = (int)e.SenderId,
                    SenderName = e.Sender.Username,
                    SenderInitials = GetInitials(e.Sender.Username),
                    Direction = isIncoming ? "in" : "out",
                    Subject = e.Subject,
                    Body = e.Body,
                    SentAt = e.SentAt,
                    Time = GetTimeAgo(e.SentAt),
                    IsRead = recipientRow?.IsRead ?? true,
                    Attachments = e.Attachments.Select(a => new InternalEmailAttachmentDto
                    {
                        Id = a.Id,
                        FileName = a.FileName,
                        FilePath = a.FilePath,
                        FileSize = a.FileSize
                    }).ToList()
                };
            })
            .ToList();

        return Ok(result);
    }

    /// <summary>
    /// Get inbox emails for current user
    /// </summary>
    [HttpGet("inbox")]
    public async Task<ActionResult<InternalEmailListResponse>> GetInbox()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var emailRecipients = await _context.EmailRecipients
            .Include(er => er.Email)
                .ThenInclude(e => e.Sender)
            .Include(er => er.Email)
                .ThenInclude(e => e.Attachments)
            .Where(er => er.RecipientId == userId && !er.Email.IsDraft)
            .OrderByDescending(er => er.Email.SentAt)
            .ToListAsync();

        var emails = emailRecipients.Select(er => MapToDto(er.Email, userId, er.IsRead, er.ReadAt)).ToList();

        return Ok(new InternalEmailListResponse
        {
            Emails = emails,
            TotalCount = emails.Count,
            UnreadCount = emails.Count(e => !e.IsRead)
        });
    }

    /// <summary>
    /// Get sent emails for current user
    /// </summary>
    [HttpGet("sent")]
    public async Task<ActionResult<InternalEmailListResponse>> GetSent()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var emails = await _context.Emails
            .Include(e => e.Sender)
            .Include(e => e.Recipients)
                .ThenInclude(r => r.Recipient)
            .Include(e => e.Attachments)
            .Where(e => e.SenderId == userId && !e.IsDraft)
            .OrderByDescending(e => e.SentAt)
            .ToListAsync();

        var emailDtos = emails.Select(e => MapToDto(e, userId, true, null)).ToList();

        return Ok(new InternalEmailListResponse
        {
            Emails = emailDtos,
            TotalCount = emailDtos.Count,
            UnreadCount = 0
        });
    }

    /// <summary>
    /// Get a specific email by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<InternalEmailDto>> GetEmail(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await GetEmailDto(id, userId));
    }

    /// <summary>
    /// Mark email as read
    /// </summary>
    [HttpPut("{id}/read")]
    public async Task<ActionResult> MarkAsRead(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var recipient = await _context.EmailRecipients
            .FirstOrDefaultAsync(er => er.EmailId == id && er.RecipientId == userId);

        if (recipient == null)
            return NotFound();

        recipient.IsRead = true;
        recipient.ReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Delete email (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteEmail(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var email = await _context.Emails
            .Include(e => e.Recipients)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (email == null)
            return NotFound();

        // Only sender or recipients can delete
        if (email.SenderId != userId && !email.Recipients.Any(r => r.RecipientId == userId))
            return Forbid();

        // For recipients, just mark as deleted (remove from their view)
        if (email.SenderId != userId)
        {
            var recipient = email.Recipients.FirstOrDefault(r => r.RecipientId == userId);
            if (recipient != null)
            {
                _context.EmailRecipients.Remove(recipient);
            }
        }
        else
        {
            // For sender, delete the entire email
            _context.Emails.Remove(email);
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<InternalEmailDto> GetEmailDto(int emailId, int currentUserId)
    {
        var email = await _context.Emails
            .Include(e => e.Sender)
            .Include(e => e.Recipients)
                .ThenInclude(r => r.Recipient)
            .Include(e => e.Attachments)
            .FirstOrDefaultAsync(e => e.Id == emailId);

        if (email == null)
            throw new Exception("Email not found");

        var recipient = email.Recipients.FirstOrDefault(r => r.RecipientId == currentUserId);
        return MapToDto(email, currentUserId, recipient?.IsRead ?? true, recipient?.ReadAt);
    }

    private InternalEmailDto MapToDto(DigitalisationERP.Core.Entities.Email email, int currentUserId, bool isRead, DateTime? readAt)
    {
        var preview = email.Body.Length > 100 ? email.Body.Substring(0, 100) + "..." : email.Body;
        var timeAgo = GetTimeAgo(email.SentAt);

        return new InternalEmailDto
        {
            Id = email.Id,
            SenderId = (int)email.SenderId,
            SenderName = email.Sender.Username,
            SenderEmail = email.Sender.Email ?? "",
            SenderInitials = GetInitials(email.Sender.Username),
            Subject = email.Subject,
            Body = email.Body,
            Preview = preview,
            SentAt = email.SentAt,
            Time = timeAgo,
            IsRead = isRead,
            ReadAt = readAt,
            Recipients = email.Recipients.Select(r => new InternalEmailRecipientDto
            {
                RecipientId = (int)r.RecipientId,
                RecipientName = r.Recipient.Username,
                RecipientEmail = r.Recipient.Email ?? "",
                Type = r.Type.ToString()
            }).ToList(),
            Attachments = email.Attachments.Select(a => new InternalEmailAttachmentDto
            {
                Id = a.Id,
                FileName = a.FileName,
                FilePath = a.FilePath,
                FileSize = a.FileSize
            }).ToList()
        };
    }

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{parts[0][0]}{parts[1][0]}".ToUpper();
        if (parts.Length == 1 && parts[0].Length > 0)
            return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();
        return "U";
    }

    private static string GetTimeAgo(DateTime dateTime)
    {
        var timeSpan = DateTime.UtcNow - dateTime;
        
        if (timeSpan.TotalMinutes < 1)
            return "Just now";
        if (timeSpan.TotalMinutes < 60)
            return $"{(int)timeSpan.TotalMinutes}m ago";
        if (timeSpan.TotalHours < 24)
            return $"{(int)timeSpan.TotalHours}h ago";
        if (timeSpan.TotalDays < 7)
            return $"{(int)timeSpan.TotalDays}d ago";
        
        return dateTime.ToString("MMM dd");
    }
}
