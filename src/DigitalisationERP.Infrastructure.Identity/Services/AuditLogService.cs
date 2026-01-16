using DigitalisationERP.Core.Abstractions;
using DigitalisationERP.Domain.Identity.Entities;
using DigitalisationERP.Infrastructure.Identity.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DigitalisationERP.Infrastructure.Identity.Services;

public class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public AuditLogService(ApplicationDbContext context, IHttpContextAccessor? httpContextAccessor = null)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
        string action,
        string tableName,
        string? recordId,
        string? oldValues,
        string? newValues,
        long? userId,
        string? ipAddress,
        string? sessionId,
        CancellationToken cancellationToken = default)
    {
        var ip = ipAddress ?? _httpContextAccessor?.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "UNKNOWN";

        var auditLog = new AuditLog
        {
            UserId = userId ?? 0,
            AuditAction = action,
            TableName = tableName,
            RecordId = recordId ?? Guid.NewGuid().ToString(),
            OldValues = oldValues,
            NewValues = newValues,
            ChangeDescription = action,
            IpAddress = ip,
            SessionId = sessionId,
            CreatedDate = DateTime.UtcNow
        };

        await _context.AuditLogs.AddAsync(auditLog, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<dynamic>> GetAuditLogsAsync(string? tableName = null, string? recordId = null, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (!string.IsNullOrEmpty(tableName))
            query = query.Where(a => a.TableName == tableName);

        if (!string.IsNullOrEmpty(recordId))
            query = query.Where(a => a.RecordId == recordId);

        if (from.HasValue)
            query = query.Where(a => a.CreatedDate >= from.Value);

        if (to.HasValue)
            query = query.Where(a => a.CreatedDate <= to.Value);

        var logs = await query
            .OrderByDescending(a => a.CreatedDate)
            .Select(a => new
            {
                a.AuditLogId,
                a.UserId,
                a.AuditAction,
                a.TableName,
                a.RecordId,
                a.OldValues,
                a.NewValues,
                a.ChangeDescription,
                a.IpAddress,
                a.SessionId,
                a.CreatedDate
            })
            .ToListAsync(cancellationToken);

        return logs.Cast<dynamic>();
    }
}
