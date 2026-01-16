using DigitalisationERP.Core.Abstractions;
using DigitalisationERP.Domain.Identity.Entities;
using DigitalisationERP.Infrastructure.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace DigitalisationERP.Infrastructure.Identity.Repositories;

public class SessionLogRepository : IRepository<SessionLog, long>
{
    private readonly ApplicationDbContext _context;

    public SessionLogRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SessionLog?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.SessionLogs
            .FirstOrDefaultAsync(s => s.SessionLogId == id, cancellationToken);
    }

    public async Task<IEnumerable<SessionLog>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SessionLogs.ToListAsync(cancellationToken);
    }

    public async Task<SessionLog?> GetBySessionTokenAsync(string sessionToken)
    {
        return await _context.SessionLogs
            .FirstOrDefaultAsync(s => s.SessionToken == sessionToken && s.IsActive);
    }

    public async Task<IEnumerable<SessionLog>> GetUserActiveSessionsAsync(long userId)
    {
        return await _context.SessionLogs
            .Where(s => s.UserId == userId && s.IsActive)
            .OrderByDescending(s => s.LoginTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<SessionLog>> GetUserSessionHistoryAsync(long userId, int days = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);
        return await _context.SessionLogs
            .Where(s => s.UserId == userId && s.LoginTime >= cutoffDate)
            .OrderByDescending(s => s.LoginTime)
            .ToListAsync();
    }

    public async Task AddAsync(SessionLog entity, CancellationToken cancellationToken = default)
    {
        await _context.SessionLogs.AddAsync(entity, cancellationToken);
    }

    public Task UpdateAsync(SessionLog entity, CancellationToken cancellationToken = default)
    {
        _context.SessionLogs.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(SessionLog entity, CancellationToken cancellationToken = default)
    {
        _context.SessionLogs.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.SessionLogs.AnyAsync(s => s.SessionLogId == id, cancellationToken);
    }

    public async Task CloseSessionAsync(string sessionToken, string? terminationReason)
    {
        var session = await GetBySessionTokenAsync(sessionToken);
        if (session != null)
        {
            session.LogoutTime = DateTime.UtcNow;
            session.IsActive = false;
            session.TerminationReason = terminationReason;
            session.SessionDuration = (int)(session.LogoutTime.Value - session.LoginTime).TotalSeconds;
            await UpdateAsync(session);
        }
    }

    public async Task TerminateAllUserSessionsAsync(long userId, string? reason = null)
    {
        var activeSessions = await GetUserActiveSessionsAsync(userId);
        foreach (var session in activeSessions)
        {
            session.LogoutTime = DateTime.UtcNow;
            session.IsActive = false;
            session.TerminationReason = reason ?? "User session terminated";
            session.SessionDuration = (int)(session.LogoutTime.Value - session.LoginTime).TotalSeconds;
            _context.SessionLogs.Update(session);
        }
    }
}
