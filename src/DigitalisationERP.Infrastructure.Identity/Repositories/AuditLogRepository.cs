using DigitalisationERP.Core.Abstractions;
using DigitalisationERP.Domain.Identity.Entities;
using DigitalisationERP.Infrastructure.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace DigitalisationERP.Infrastructure.Identity.Repositories;

public class AuditLogRepository : IRepository<AuditLog, long>
{
    private readonly ApplicationDbContext _context;

    public AuditLogRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AuditLog?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .FirstOrDefaultAsync(a => a.AuditLogId == id, cancellationToken);
    }

    public async Task<IEnumerable<AuditLog>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .OrderByDescending(a => a.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<AuditLog>> GetUserAuditLogsAsync(long userId, int days = 90)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);
        return await _context.AuditLogs
            .Where(a => a.UserId == userId && a.CreatedDate >= cutoffDate)
            .OrderByDescending(a => a.CreatedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetTableAuditLogsAsync(string tableName, int days = 90)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);
        return await _context.AuditLogs
            .Where(a => a.TableName == tableName && a.CreatedDate >= cutoffDate)
            .OrderByDescending(a => a.CreatedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetRecordAuditLogsAsync(string tableName, string recordId)
    {
        return await _context.AuditLogs
            .Where(a => a.TableName == tableName && a.RecordId == recordId)
            .OrderByDescending(a => a.CreatedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetActionAuditLogsAsync(string auditAction, int days = 90)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);
        return await _context.AuditLogs
            .Where(a => a.AuditAction == auditAction && a.CreatedDate >= cutoffDate)
            .OrderByDescending(a => a.CreatedDate)
            .ToListAsync();
    }

    public async Task AddAsync(AuditLog entity, CancellationToken cancellationToken = default)
    {
        await _context.AuditLogs.AddAsync(entity, cancellationToken);
    }

    public Task UpdateAsync(AuditLog entity, CancellationToken cancellationToken = default)
    {
        _context.AuditLogs.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(AuditLog entity, CancellationToken cancellationToken = default)
    {
        _context.AuditLogs.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs.AnyAsync(a => a.AuditLogId == id, cancellationToken);
    }

    public async Task<int> CleanupOldAuditLogsAsync(int retentionDays = 180)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        var oldLogs = await _context.AuditLogs
            .Where(a => a.CreatedDate < cutoffDate)
            .ToListAsync();

        _context.AuditLogs.RemoveRange(oldLogs);
        return oldLogs.Count;
    }
}
