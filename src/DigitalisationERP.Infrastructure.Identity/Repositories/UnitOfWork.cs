using DigitalisationERP.Core.Abstractions;
using DigitalisationERP.Domain.Identity.Entities;
using DigitalisationERP.Infrastructure.Identity.Data;

namespace DigitalisationERP.Infrastructure.Identity.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private UserRepository? _userRepository;
    private RoleRepository? _roleRepository;
    private AuthorizationRepository? _authorizationRepository;
    private PasswordHistoryRepository? _passwordHistoryRepository;
    private SessionLogRepository? _sessionLogRepository;
    private AuditLogRepository? _auditLogRepository;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
    }

    public IRepository<User, long> Users => _userRepository ??= new UserRepository(_context);
    public IRepository<Role, long> Roles => _roleRepository ??= new RoleRepository(_context);
    public IRepository<AuthorizationObject, long> Authorizations => _authorizationRepository ??= new AuthorizationRepository(_context);
    public IRepository<PasswordHistory, long> PasswordHistories => _passwordHistoryRepository ??= new PasswordHistoryRepository(_context);
    public IRepository<SessionLog, long> SessionLogs => _sessionLogRepository ??= new SessionLogRepository(_context);
    public IRepository<AuditLog, long> AuditLogs => _auditLogRepository ??= new AuditLogRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction != null)
            return;

        await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_context.Database.CurrentTransaction != null)
            {
                await _context.SaveChangesAsync(cancellationToken);
                await _context.Database.CommitTransactionAsync(cancellationToken);
            }
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_context.Database.CurrentTransaction != null)
            {
                await _context.Database.RollbackTransactionAsync(cancellationToken);
            }
        }
        catch
        {
            // Transaction rollback failed, log if needed
        }
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_context != null)
            await _context.DisposeAsync();
    }
}
