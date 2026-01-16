using DigitalisationERP.Core.Abstractions;
using DigitalisationERP.Domain.Identity.Entities;
using DigitalisationERP.Infrastructure.Identity.Data;
using DigitalisationERP.Infrastructure.Identity.Repositories;
using DigitalisationERP.Infrastructure.Identity.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DigitalisationERP.Infrastructure.Identity;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureLayer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // DbContext
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

            options.UseSqlite(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
            });
        });

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Repositories
        services.AddScoped<UserRepository>();
        services.AddScoped<IRepository<User, long>>(sp => sp.GetRequiredService<UserRepository>());
        services.AddScoped<RoleRepository>();
        services.AddScoped<AuthorizationRepository>();
        services.AddScoped<PasswordHistoryRepository>();
        services.AddScoped<SessionLogRepository>();
        services.AddScoped<AuditLogRepository>();

        // Services
        services.AddScoped<IPasswordHashingService, PasswordHashingService>();
        services.AddScoped<PasswordHashingService>();
        services.AddScoped<Services.ITokenService, TokenService>();
        services.AddScoped<TokenService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        return services;
    }
}
