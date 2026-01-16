using DigitalisationERP.Core.Abstractions;
using DigitalisationERP.Domain.Identity.Entities;
using DigitalisationERP.Domain.Identity.ValueObjects;
using DigitalisationERP.Infrastructure.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace DigitalisationERP.Infrastructure.Identity.Repositories;

public class UserRepository : IRepository<User, long>
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.UserRoleAssignments)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.UserGroupAssignments)
                .ThenInclude(ug => ug.UserGroup)
            .Include(u => u.PasswordHistoryEntries)
            .Include(u => u.SessionLogs)
            .FirstOrDefaultAsync(u => u.UserId == id, cancellationToken);
    }

    public async Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.UserRoleAssignments)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.UserGroupAssignments)
                .ThenInclude(ug => ug.UserGroup)
            .ToListAsync(cancellationToken);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _context.Users
            .Include(u => u.UserRoleAssignments)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Username.Value == username);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .Include(u => u.UserRoleAssignments)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email.Value == email);
    }

    public async Task AddAsync(User entity, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(entity, cancellationToken);
    }

    public Task UpdateAsync(User entity, CancellationToken cancellationToken = default)
    {
        _context.Users.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(User entity, CancellationToken cancellationToken = default)
    {
        _context.Users.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.Users.AnyAsync(u => u.UserId == id, cancellationToken);
    }

    public async Task<bool> UsernameExistsAsync(string username)
    {
        return await _context.Users.AnyAsync(u => u.Username.Value == username);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Users.AnyAsync(u => u.Email.Value == email);
    }
}
