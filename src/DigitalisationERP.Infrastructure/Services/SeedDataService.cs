using DigitalisationERP.Core.Entities.Auth;
using DigitalisationERP.Core.Enums;
using DigitalisationERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BCrypt.Net;

namespace DigitalisationERP.Infrastructure.Services;

public interface ISeedDataService
{
    Task SeedAsync();
}

public class SeedDataService : ISeedDataService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SeedDataService> _logger;

    public SeedDataService(ApplicationDbContext context, ILogger<SeedDataService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            _logger.LogInformation("Starting data seeding...");

            // Seed Roles
            await SeedRolesAsync();

            // Seed Authorization Objects
            await SeedAuthorizationsAsync();

            // Seed Default Admin User
            await SeedAdminUserAsync();

            // Optionally seed a configured S_USER account (via env vars)
            await SeedConfiguredStandardUserAsync();

            _logger.LogInformation("Data seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while seeding data");
            throw;
        }
    }

    private async Task SeedRolesAsync()
    {
        _logger.LogInformation("Seeding roles...");

        var roles = new List<Role>
        {
            // Basic
            new Role
            {
                RoleName = "S_USER",
                DisplayName = "Standard User",
                Description = "Standard end-user access",
                RoleType = RoleType.Standard,
                Module = "BASIS",
                IsActive = true,
                ClientId = "001"
            },
            // System Administration
            new Role
            {
                RoleName = "SAP_ALL",
                DisplayName = "System Administrator",
                Description = "Full system access - all authorizations",
                RoleType = RoleType.Standard,
                Module = "BASIS",
                IsActive = true,
                ClientId = "001"
            },
            new Role
            {
                RoleName = "SAP_BASIS",
                DisplayName = "Basis Administrator",
                Description = "System configuration and technical administration",
                RoleType = RoleType.Standard,
                Module = "BASIS",
                IsActive = true,
                ClientId = "001"
            },

            // Production Planning (PP)
            new Role
            {
                RoleName = "Z_PROD_MANAGER",
                DisplayName = "Production Manager",
                Description = "Full production planning and control",
                RoleType = RoleType.Custom,
                Module = "PP",
                IsActive = true,
                ClientId = "001"
            },
            new Role
            {
                RoleName = "Z_PROD_PLANNER",
                DisplayName = "Production Planner",
                Description = "Create and schedule production orders",
                RoleType = RoleType.Custom,
                Module = "PP",
                IsActive = true,
                ClientId = "001"
            },
            new Role
            {
                RoleName = "Z_PROD_OPERATOR",
                DisplayName = "Production Operator",
                Description = "Execute and confirm production orders",
                RoleType = RoleType.Custom,
                Module = "PP",
                IsActive = true,
                ClientId = "001"
            },

            // Plant Maintenance (PM)
            new Role
            {
                RoleName = "Z_MAINT_MANAGER",
                DisplayName = "Maintenance Manager",
                Description = "Full maintenance planning and management",
                RoleType = RoleType.Custom,
                Module = "PM",
                IsActive = true,
                ClientId = "001"
            },
            new Role
            {
                RoleName = "Z_MAINT_PLANNER",
                DisplayName = "Maintenance Planner",
                Description = "Plan and schedule maintenance activities",
                RoleType = RoleType.Custom,
                Module = "PM",
                IsActive = true,
                ClientId = "001"
            },
            new Role
            {
                RoleName = "Z_MAINT_TECH",
                DisplayName = "Maintenance Technician",
                Description = "Execute maintenance orders and record activities",
                RoleType = RoleType.Custom,
                Module = "PM",
                IsActive = true,
                ClientId = "001"
            },

            // Materials Management (MM)
            new Role
            {
                RoleName = "Z_WM_MANAGER",
                DisplayName = "Warehouse Manager",
                Description = "Full inventory and warehouse management",
                RoleType = RoleType.Custom,
                Module = "MM",
                IsActive = true,
                ClientId = "001"
            },
            new Role
            {
                RoleName = "Z_WM_CLERK",
                DisplayName = "Warehouse Clerk",
                Description = "Execute stock movements and inventory transactions",
                RoleType = RoleType.Custom,
                Module = "MM",
                IsActive = true,
                ClientId = "001"
            },
            new Role
            {
                RoleName = "Z_MM_BUYER",
                DisplayName = "Material Buyer",
                Description = "Create and manage purchase orders",
                RoleType = RoleType.Custom,
                Module = "MM",
                IsActive = true,
                ClientId = "001"
            },

            // Quality Management (QM)
            new Role
            {
                RoleName = "Z_QM_MANAGER",
                DisplayName = "Quality Manager",
                Description = "Quality planning and management",
                RoleType = RoleType.Custom,
                Module = "QM",
                IsActive = true,
                ClientId = "001"
            },
            new Role
            {
                RoleName = "Z_QM_INSPECTOR",
                DisplayName = "Quality Inspector",
                Description = "Perform quality inspections and record results",
                RoleType = RoleType.Custom,
                Module = "QM",
                IsActive = true,
                ClientId = "001"
            },

            // Robotics & IoT
            new Role
            {
                RoleName = "Z_ROBOT_ADMIN",
                DisplayName = "Robot Administrator",
                Description = "Configure and manage robot systems",
                RoleType = RoleType.Custom,
                Module = "ROBOTICS",
                IsActive = true,
                ClientId = "001"
            },
            new Role
            {
                RoleName = "Z_ROBOT_OPERATOR",
                DisplayName = "Robot Operator",
                Description = "Control and monitor robot operations",
                RoleType = RoleType.Custom,
                Module = "ROBOTICS",
                IsActive = true,
                ClientId = "001"
            },
            new Role
            {
                RoleName = "Z_IOT_ADMIN",
                DisplayName = "IoT Administrator",
                Description = "Manage sensors and IoT devices",
                RoleType = RoleType.Custom,
                Module = "IOT",
                IsActive = true,
                ClientId = "001"
            },
            new Role
            {
                RoleName = "Z_IOT_MONITOR",
                DisplayName = "IoT Monitor",
                Description = "View sensor data and alerts",
                RoleType = RoleType.Custom,
                Module = "IOT",
                IsActive = true,
                ClientId = "001"
            }
        };

        foreach (var role in roles)
        {
            if (!await _context.Roles.AnyAsync(r => r.RoleName == role.RoleName && r.ClientId == role.ClientId))
            {
                await _context.Roles.AddAsync(role);
                _logger.LogInformation("Added role: {RoleName}", role.RoleName);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedAuthorizationsAsync()
    {
        _logger.LogInformation("Seeding authorization objects...");

        var authObjects = new List<Authorization>
        {
            // Materials Management
            new Authorization
            {
                ObjectCode = "M_MATE_WRK",
                DisplayName = "Material Master",
                Description = "Authorization for material master data",
                Module = "MM",
                IsStandard = true,
                IsActive = true,
                ClientId = "001",
                Fields = new List<AuthorizationField>
                {
                    new AuthorizationField { FieldName = "ACTVT", DisplayName = "Activity", Description = "Activity (01=Create, 02=Change, 03=Display, 06=Delete)", DataType = "CHAR", IsMandatory = true },
                    new AuthorizationField { FieldName = "WERKS", DisplayName = "Plant", Description = "Plant", DataType = "CHAR", IsMandatory = false },
                    new AuthorizationField { FieldName = "MTART", DisplayName = "Material Type", Description = "Material Type", DataType = "CHAR", IsMandatory = false }
                }
            },
            new Authorization
            {
                ObjectCode = "M_MSEG_WMB",
                DisplayName = "Material Document",
                Description = "Authorization for goods movements",
                Module = "MM",
                IsStandard = true,
                IsActive = true,
                ClientId = "001",
                Fields = new List<AuthorizationField>
                {
                    new AuthorizationField { FieldName = "ACTVT", DisplayName = "Activity", Description = "Activity", DataType = "CHAR", IsMandatory = true },
                    new AuthorizationField { FieldName = "BWART", DisplayName = "Movement Type", Description = "Movement Type (101=GR, 261=GI, 311=Transfer)", DataType = "CHAR", IsMandatory = true },
                    new AuthorizationField { FieldName = "WERKS", DisplayName = "Plant", Description = "Plant", DataType = "CHAR", IsMandatory = false }
                }
            },

            // Production Planning
            new Authorization
            {
                ObjectCode = "P_ORDR",
                DisplayName = "Production Order",
                Description = "Authorization for production orders",
                Module = "PP",
                IsStandard = true,
                IsActive = true,
                ClientId = "001",
                Fields = new List<AuthorizationField>
                {
                    new AuthorizationField { FieldName = "ACTVT", DisplayName = "Activity", Description = "Activity", DataType = "CHAR", IsMandatory = true },
                    new AuthorizationField { FieldName = "WERKS", DisplayName = "Plant", Description = "Plant", DataType = "CHAR", IsMandatory = false },
                    new AuthorizationField { FieldName = "AUART", DisplayName = "Order Type", Description = "Order Type", DataType = "CHAR", IsMandatory = false }
                }
            },

            // Plant Maintenance
            new Authorization
            {
                ObjectCode = "I_ORDERC",
                DisplayName = "Maintenance Order",
                Description = "Authorization for maintenance orders",
                Module = "PM",
                IsStandard = true,
                IsActive = true,
                ClientId = "001",
                Fields = new List<AuthorizationField>
                {
                    new AuthorizationField { FieldName = "ACTVT", DisplayName = "Activity", Description = "Activity", DataType = "CHAR", IsMandatory = true },
                    new AuthorizationField { FieldName = "IWERK", DisplayName = "Maintenance Plant", Description = "Maintenance Plant", DataType = "CHAR", IsMandatory = false },
                    new AuthorizationField { FieldName = "AUART", DisplayName = "Order Type", Description = "Order Type", DataType = "CHAR", IsMandatory = false }
                }
            },

            // Robotics Control
            new Authorization
            {
                ObjectCode = "Z_ROBOT",
                DisplayName = "Robot Control",
                Description = "Authorization for robot operations",
                Module = "ROBOTICS",
                IsStandard = false,
                IsActive = true,
                ClientId = "001",
                Fields = new List<AuthorizationField>
                {
                    new AuthorizationField { FieldName = "ACTVT", DisplayName = "Activity", Description = "Activity (01=Create, 02=Change, 03=Display, 08=Execute)", DataType = "CHAR", IsMandatory = true },
                    new AuthorizationField { FieldName = "ROBOT_ID", DisplayName = "Robot ID", Description = "Robot ID", DataType = "CHAR", IsMandatory = false },
                    new AuthorizationField { FieldName = "TASK_TYPE", DisplayName = "Task Type", Description = "Task Type (PICK, PLACE, MOVE, STOP)", DataType = "CHAR", IsMandatory = true }
                }
            },

            // IoT Management
            new Authorization
            {
                ObjectCode = "Z_IOT",
                DisplayName = "IoT Device Management",
                Description = "Authorization for IoT devices and sensors",
                Module = "IOT",
                IsStandard = false,
                IsActive = true,
                ClientId = "001",
                Fields = new List<AuthorizationField>
                {
                    new AuthorizationField { FieldName = "ACTVT", DisplayName = "Activity", Description = "Activity", DataType = "CHAR", IsMandatory = true },
                    new AuthorizationField { FieldName = "DEVICE_TYPE", DisplayName = "Device Type", Description = "Device Type (SENSOR, ACTUATOR, GATEWAY)", DataType = "CHAR", IsMandatory = false },
                    new AuthorizationField { FieldName = "ALERT_LEVEL", DisplayName = "Alert Level", Description = "Alert Level (INFO, WARNING, CRITICAL)", DataType = "CHAR", IsMandatory = false }
                }
            }
        };

        foreach (var auth in authObjects)
        {
            if (!await _context.Authorizations.AnyAsync(a => a.ObjectCode == auth.ObjectCode && a.ClientId == auth.ClientId))
            {
                await _context.Authorizations.AddAsync(auth);
                _logger.LogInformation("Added authorization object: {ObjectCode}", auth.ObjectCode);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedAdminUserAsync()
    {
        var seedAdmin = Environment.GetEnvironmentVariable("ERP_SEED_ADMIN");
        if (!string.Equals(seedAdmin, "true", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Admin seeding disabled (set ERP_SEED_ADMIN=true to enable)");
            return;
        }

        var adminUsername = Environment.GetEnvironmentVariable("ERP_ADMIN_USERNAME") ?? "admin";
        var adminEmail = Environment.GetEnvironmentVariable("ERP_ADMIN_EMAIL") ?? "admin@erp.local";
        var adminPassword = Environment.GetEnvironmentVariable("ERP_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            _logger.LogWarning("Admin seeding enabled but ERP_ADMIN_PASSWORD is missing; skipping admin creation");
            return;
        }

        _logger.LogInformation("Seeding admin user (if missing)...");

        if (await _context.Users.AnyAsync(u => u.Username == adminUsername || u.Email == adminEmail))
        {
            _logger.LogInformation("Admin user already exists");
            return;
        }

        var adminUser = new User
        {
            Username = adminUsername,
            Email = adminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
            FirstName = "System",
            LastName = "Administrator",
            UserType = UserType.Dialog,
            Status = UserStatus.Active,
            EmployeeNumber = "ADM001",
            Department = "IT",
            Language = "EN",
            ValidFrom = DateTime.UtcNow,
            ValidTo = DateTime.MaxValue,
            ClientId = "001",
            PasswordLastChanged = DateTime.UtcNow
        };

        await _context.Users.AddAsync(adminUser);
        await _context.SaveChangesAsync();

        // Assign SAP_ALL role to admin
        var sapAllRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "SAP_ALL");
        if (sapAllRole != null)
        {
            var userRole = new UserRole
            {
                UserId = adminUser.Id,
                RoleId = sapAllRole.Id,
                ValidFrom = DateTime.UtcNow,
                ValidTo = DateTime.MaxValue,
                ClientId = "001"
            };

            await _context.UserRoles.AddAsync(userRole);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin user created with SAP_ALL role");
            _logger.LogInformation("Username: {Username}", adminUsername);
        }
    }

    private async Task SeedConfiguredStandardUserAsync()
    {
        var email = Environment.GetEnvironmentVariable("ERP_SEED_SUSER_EMAIL");
        var password = Environment.GetEnvironmentVariable("ERP_SEED_SUSER_PASSWORD");

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogInformation("S_USER seeding not configured (set ERP_SEED_SUSER_EMAIL and ERP_SEED_SUSER_PASSWORD)");
            return;
        }

        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (existingUser == null)
        {
            existingUser = new User
            {
                Username = email.Split('@')[0],
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                FirstName = "Standard",
                LastName = "User",
                EmailVerified = true,
                Status = UserStatus.Active,
                UserType = UserType.Dialog,
                ValidFrom = DateTime.UtcNow,
                ValidTo = DateTime.MaxValue,
                ClientId = "001",
                PasswordLastChanged = DateTime.UtcNow,
                MustChangePassword = false,
                FailedLoginAttempts = 0
            };

            await _context.Users.AddAsync(existingUser);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created configured S_USER account: {Email}", email);
        }

        var sUserRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "S_USER" && r.ClientId == "001");
        if (sUserRole == null)
        {
            _logger.LogWarning("S_USER role not found; cannot assign role to {Email}", email);
            return;
        }

        var hasUserRole = await _context.UserRoles.AnyAsync(ur => ur.UserId == existingUser.Id && ur.RoleId == sUserRole.Id);
        if (!hasUserRole)
        {
            await _context.UserRoles.AddAsync(new UserRole
            {
                UserId = existingUser.Id,
                RoleId = sUserRole.Id,
                ValidFrom = DateTime.UtcNow,
                ValidTo = DateTime.MaxValue,
                ClientId = "001"
            });
            await _context.SaveChangesAsync();
            _logger.LogInformation("Assigned S_USER role to {Email}", email);
        }
    }
}
