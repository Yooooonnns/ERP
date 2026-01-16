using DigitalisationERP.Domain.Identity.Entities;
using DigitalisationERP.Infrastructure.Identity.Data;

namespace DigitalisationERP.API.Services;

/// <summary>
/// Service for seeding initial data into the database.
/// </summary>
public class DataSeeder : IDataSeeder
{
    private readonly ILogger<DataSeeder> _logger;

    /// <summary>
    /// Initializes a new instance of the DataSeeder class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public DataSeeder(ILogger<DataSeeder> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Seeds the database with initial data if it's empty.
    /// </summary>
    /// <param name="context">The application database context.</param>
    public async Task SeedAsync(ApplicationDbContext context)
    {
        try
        {
            // Check if roles already exist (idempotent)
            if (context.Roles.Any())
            {
                _logger.LogInformation("Roles already exist. Skipping role seeding.");
                return;
            }

            // Create SAP standard roles with hierarchy using factory methods
            var directorRole = Role.CreateSingle("DIRECTOR", "Directeur", "Role Director - Top level management");
            var prodMgrRole = Role.CreateDerived("PROD_MGR", "Manager Production", directorRole, "Production Manager - Manages production operations");
            var shiftLeadRole = Role.CreateDerived("SHIFT_LEAD", "Chef d'équipe", prodMgrRole, "Shift Leader - Manages shift operations and team");
            var operatorRole = Role.CreateDerived("OPERATOR", "Opérateur", shiftLeadRole, "Production Operator - Operates production equipment");
            
            var maintMgrRole = Role.CreateDerived("MAINT_MGR", "Manager Maintenance", directorRole, "Maintenance Manager - Manages maintenance operations");
            var maintTechRole = Role.CreateDerived("MAINT_TECH", "Technicien Maintenance", maintMgrRole, "Maintenance Technician - Performs maintenance tasks");
            
            var warehouseMgrRole = Role.CreateDerived("MAGAZINE", "Magasinier", directorRole, "Warehouse Manager - Manages warehouse operations");
            var warehouseWorkerRole = Role.CreateDerived("WAREHOUSE", "Manutentionnaire", warehouseMgrRole, "Warehouse Worker - Performs warehouse tasks");

            var roles = new List<Role> 
            { 
                directorRole,
                prodMgrRole, 
                shiftLeadRole, 
                operatorRole, 
                maintMgrRole, 
                maintTechRole, 
                warehouseMgrRole, 
                warehouseWorkerRole 
            };

            // Add roles to context
            await context.Roles.AddRangeAsync(roles);
            await context.SaveChangesAsync();

            _logger.LogInformation("Roles seeded successfully. Total roles added: {RoleCount}", roles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding roles");
            throw;
        }
    }
}
