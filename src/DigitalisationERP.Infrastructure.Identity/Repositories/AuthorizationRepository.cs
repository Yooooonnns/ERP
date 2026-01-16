using DigitalisationERP.Core.Abstractions;
using DigitalisationERP.Domain.Identity.Entities;
using DigitalisationERP.Infrastructure.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace DigitalisationERP.Infrastructure.Identity.Repositories;

public class AuthorizationRepository : IRepository<AuthorizationObject, long>
{
    private readonly ApplicationDbContext _context;

    public AuthorizationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AuthorizationObject?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.AuthorizationObjects
            .Include(a => a.AuthorizationFields)
            .Include(a => a.RoleAuthorizations)
                .ThenInclude(ra => ra.Role)
            .FirstOrDefaultAsync(a => a.AuthObjectId == id, cancellationToken);
    }

    public async Task<IEnumerable<AuthorizationObject>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.AuthorizationObjects
            .Include(a => a.AuthorizationFields)
            .Where(a => a.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<AuthorizationObject?> GetByCodeAsync(string code)
    {
        return await _context.AuthorizationObjects
            .Include(a => a.AuthorizationFields)
            .FirstOrDefaultAsync(a => a.AuthObjectCode == code);
    }

    public async Task<IEnumerable<RoleAuthorization>> GetRoleAuthorizationsAsync(long roleId)
    {
        return await _context.RoleAuthorizations
            .Where(ra => ra.RoleId == roleId && ra.IsActive)
            .Include(ra => ra.AuthorizationObject!)
                .ThenInclude(ao => ao.AuthorizationFields)
            .ToListAsync();
    }

    public async Task AddAsync(AuthorizationObject entity, CancellationToken cancellationToken = default)
    {
        await _context.AuthorizationObjects.AddAsync(entity, cancellationToken);
    }

    public Task UpdateAsync(AuthorizationObject entity, CancellationToken cancellationToken = default)
    {
        _context.AuthorizationObjects.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(AuthorizationObject entity, CancellationToken cancellationToken = default)
    {
        _context.AuthorizationObjects.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.AuthorizationObjects.AnyAsync(a => a.AuthObjectId == id, cancellationToken);
    }
}
