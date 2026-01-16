using DigitalisationERP.Core.Abstractions;
using DigitalisationERP.Domain.Identity.Entities;
using DigitalisationERP.Infrastructure.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace DigitalisationERP.Infrastructure.Identity.Repositories;

public class RoleRepository : IRepository<Role, long>
{
    private readonly ApplicationDbContext _context;

    public RoleRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Role?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .Include(r => r.UserRoleAssignments)
            .Include(r => r.RoleAuthorizations)
                .ThenInclude(ra => ra.AuthorizationObject)
            .Include(r => r.ParentRole)
            .Include(r => r.ChildRoles)
            .FirstOrDefaultAsync(r => r.RoleId == id, cancellationToken);
    }

    public async Task<IEnumerable<Role>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .Include(r => r.RoleAuthorizations)
                .ThenInclude(ra => ra.AuthorizationObject)
            .Include(r => r.ParentRole)
            .Include(r => r.ChildRoles)
            .ToListAsync(cancellationToken);
    }

    public async Task<Role?> GetByRoleCodeAsync(string roleCode)
    {
        return await _context.Roles
            .Include(r => r.RoleAuthorizations)
                .ThenInclude(ra => ra.AuthorizationObject)
            .Include(r => r.ParentRole)
            .Include(r => r.ChildRoles)
            .FirstOrDefaultAsync(r => r.RoleCode == roleCode);
    }

    public async Task<IEnumerable<Role>> GetHierarchyByParentAsync(long? parentRoleId)
    {
        return await _context.Roles
            .Where(r => r.ParentRoleId == parentRoleId && r.IsActive)
            .Include(r => r.ChildRoles)
            .ToListAsync();
    }

    public async Task AddAsync(Role entity, CancellationToken cancellationToken = default)
    {
        await _context.Roles.AddAsync(entity, cancellationToken);
    }

    public Task UpdateAsync(Role entity, CancellationToken cancellationToken = default)
    {
        _context.Roles.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Role entity, CancellationToken cancellationToken = default)
    {
        _context.Roles.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.Roles.AnyAsync(r => r.RoleId == id, cancellationToken);
    }

    public async Task<bool> RoleCodeExistsAsync(string roleCode)
    {
        return await _context.Roles.AnyAsync(r => r.RoleCode == roleCode);
    }
}
