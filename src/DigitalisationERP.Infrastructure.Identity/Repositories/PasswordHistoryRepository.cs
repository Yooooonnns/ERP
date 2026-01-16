using DigitalisationERP.Core.Abstractions;
using DigitalisationERP.Domain.Identity.Entities;
using DigitalisationERP.Infrastructure.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace DigitalisationERP.Infrastructure.Identity.Repositories;

public class PasswordHistoryRepository : IRepository<PasswordHistory, long>
{
    private readonly ApplicationDbContext _context;

    public PasswordHistoryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PasswordHistory?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.PasswordHistories
            .FirstOrDefaultAsync(p => p.PasswordHistoryId == id, cancellationToken);
    }

    public async Task<IEnumerable<PasswordHistory>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.PasswordHistories.ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<PasswordHistory>> GetUserPasswordHistoryAsync(long userId, int limit = 5)
    {
        return await _context.PasswordHistories
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedDate)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<bool> IsPasswordReusedAsync(long userId, string passwordHash)
    {
        return await _context.PasswordHistories
            .Where(p => p.UserId == userId)
            .AnyAsync(p => p.PasswordHash == passwordHash);
    }

    public async Task AddAsync(PasswordHistory entity, CancellationToken cancellationToken = default)
    {
        await _context.PasswordHistories.AddAsync(entity, cancellationToken);
    }

    public Task UpdateAsync(PasswordHistory entity, CancellationToken cancellationToken = default)
    {
        _context.PasswordHistories.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(PasswordHistory entity, CancellationToken cancellationToken = default)
    {
        _context.PasswordHistories.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.PasswordHistories.AnyAsync(p => p.PasswordHistoryId == id, cancellationToken);
    }

    public async Task CleanupExpiredPasswordHistoryAsync(long userId)
    {
        var expiredHistories = await _context.PasswordHistories
            .Where(p => p.UserId == userId && p.ExpiryDate < DateTime.UtcNow)
            .ToListAsync();

        _context.PasswordHistories.RemoveRange(expiredHistories);
    }
}
