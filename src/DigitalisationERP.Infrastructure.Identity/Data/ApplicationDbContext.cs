using DigitalisationERP.Domain.Identity.Entities;
using DigitalisationERP.Domain.Identity.Enums;
using DigitalisationERP.Domain.Identity.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace DigitalisationERP.Infrastructure.Identity.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    // DbSets
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<UserRoleAssignment> UserRoleAssignments { get; set; } = null!;
    public DbSet<UserGroup> UserGroups { get; set; } = null!;
    public DbSet<UserGroupAssignment> UserGroupAssignments { get; set; } = null!;
    public DbSet<AuthorizationObject> AuthorizationObjects { get; set; } = null!;
    public DbSet<AuthorizationField> AuthorizationFields { get; set; } = null!;
    public DbSet<RoleAuthorization> RoleAuthorizations { get; set; } = null!;
    public DbSet<PasswordHistory> PasswordHistories { get; set; } = null!;
    public DbSet<SessionLog> SessionLogs { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).ValueGeneratedOnAdd();

            entity.Property(e => e.Username)
                .HasMaxLength(128)
                .IsRequired();
            entity.HasIndex(e => e.Username).IsUnique();

            // Ignore Username and Password value objects - use shadow properties
            entity.Ignore(e => e.Username);
            entity.Ignore(e => e.Password);

            // Add shadow properties for username and password
            entity.Property<string>("UsernameValue")
                .HasColumnName("Username")
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(e => e.Email)
                .HasConversion(
                    e => e.Value,
                    e => Email.Create(e))
                .HasMaxLength(256)
                .IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();

            // Add shadow properties for password
            entity.Property<string>("PasswordHash").HasMaxLength(256).IsRequired();
            entity.Property<string>("PasswordSalt").HasMaxLength(256).IsRequired();

            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);

            entity.Property(e => e.UserType)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsLocked).HasDefaultValue(false);

            entity.Property(e => e.FailedLoginAttempts).HasDefaultValue(0);

            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();

            entity.Property(e => e.ModifiedDate)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();

            entity.Property(e => e.ModifiedBy).HasMaxLength(128);

            // Relationships
            entity.HasMany(e => e.UserRoleAssignments)
                .WithOne(u => u.User)
                .HasForeignKey(u => u.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.UserGroupAssignments)
                .WithOne(u => u.User)
                .HasForeignKey(u => u.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.PasswordHistoryEntries)
                .WithOne(p => p.User)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.SessionLogs)
                .WithOne(s => s.User)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Role entity
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId);
            entity.Property(e => e.RoleId).ValueGeneratedOnAdd();

            entity.Property(e => e.RoleCode)
                .HasMaxLength(128)
                .IsRequired();
            entity.HasIndex(e => e.RoleCode).IsUnique();

            entity.Property(e => e.RoleName)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(e => e.Description).HasMaxLength(500);

            entity.Property(e => e.RoleType)
                .HasMaxLength(50)
                .HasDefaultValue("SingleRole")
                .IsRequired();

            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();

            entity.Property(e => e.ModifiedDate)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();

            // Self-referencing relationship for role hierarchy
            entity.HasOne(e => e.ParentRole)
                .WithMany(r => r.ChildRoles)
                .HasForeignKey(e => e.ParentRoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationships
            entity.HasMany(e => e.UserRoleAssignments)
                .WithOne(u => u.Role)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.RoleAuthorizations)
                .WithOne(r => r.Role)
                .HasForeignKey(r => r.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure UserRoleAssignment
        modelBuilder.Entity<UserRoleAssignment>(entity =>
        {
            entity.HasKey(e => e.UserRoleAssignmentId);
            entity.Property(e => e.UserRoleAssignmentId).ValueGeneratedOnAdd();

            entity.HasIndex(e => new { e.UserId, e.RoleId }).IsUnique();

            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();

            entity.Property(e => e.ModifiedDate)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();

            entity.Property(e => e.ModifiedBy).HasMaxLength(128);
        });

        // Configure UserGroup
        modelBuilder.Entity<UserGroup>(entity =>
        {
            entity.HasKey(e => e.UserGroupId);
            entity.Property(e => e.UserGroupId).ValueGeneratedOnAdd();

            entity.Property(e => e.UserGroupCode)
                .HasMaxLength(128)
                .IsRequired();
            entity.HasIndex(e => e.UserGroupCode).IsUnique();

            entity.Property(e => e.UserGroupName)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(e => e.Description).HasMaxLength(500);

            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();

            entity.Property(e => e.ModifiedDate)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();

            entity.HasMany(e => e.UserGroupAssignments)
                .WithOne(u => u.UserGroup)
                .HasForeignKey(u => u.UserGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure UserGroupAssignment
        modelBuilder.Entity<UserGroupAssignment>(entity =>
        {
            entity.HasKey(e => e.UserGroupAssignmentId);
            entity.Property(e => e.UserGroupAssignmentId).ValueGeneratedOnAdd();

            entity.HasIndex(e => new { e.UserId, e.UserGroupId }).IsUnique();

            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();

            entity.Property(e => e.ModifiedDate)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();

            entity.Property(e => e.ModifiedBy).HasMaxLength(128);
        });

        // Configure AuthorizationObject
        modelBuilder.Entity<AuthorizationObject>(entity =>
        {
            entity.HasKey(e => e.AuthObjectId);
            entity.Property(e => e.AuthObjectId).ValueGeneratedOnAdd();

            entity.Property(e => e.AuthObjectCode)
                .HasMaxLength(128)
                .IsRequired();
            entity.HasIndex(e => e.AuthObjectCode).IsUnique();

            entity.Property(e => e.AuthObjectName)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(e => e.Description).HasMaxLength(500);

            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();

            entity.Property(e => e.ModifiedDate)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();

            entity.HasMany(e => e.AuthorizationFields)
                .WithOne(f => f.AuthorizationObject)
                .HasForeignKey(f => f.AuthObjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.RoleAuthorizations)
                .WithOne(r => r.AuthorizationObject)
                .HasForeignKey(r => r.AuthObjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure AuthorizationField
        modelBuilder.Entity<AuthorizationField>(entity =>
        {
            entity.HasKey(e => e.AuthFieldId);
            entity.Property(e => e.AuthFieldId).ValueGeneratedOnAdd();

            entity.Property(e => e.FieldName)
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(e => e.FieldDescription).HasMaxLength(500);

            entity.Property(e => e.DataType)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.IsRequired).HasDefaultValue(false);

            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();

            entity.Property(e => e.ModifiedDate)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();
        });

        // Configure RoleAuthorization
        modelBuilder.Entity<RoleAuthorization>(entity =>
        {
            entity.HasKey(e => e.RoleAuthId);
            entity.Property(e => e.RoleAuthId).ValueGeneratedOnAdd();

            entity.HasIndex(e => new { e.RoleId, e.AuthObjectId }).IsUnique();

            entity.Property(e => e.Activity)
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(e => e.FieldValues)
                .HasColumnType("nvarchar(max)")
                .IsRequired(false);

            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();

            entity.Property(e => e.ModifiedDate)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();

            entity.Property(e => e.ModifiedBy).HasMaxLength(128);
        });

        // Configure PasswordHistory
        modelBuilder.Entity<PasswordHistory>(entity =>
        {
            entity.HasKey(e => e.PasswordHistoryId);
            entity.Property(e => e.PasswordHistoryId).ValueGeneratedOnAdd();

            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.PasswordSalt)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();
        });

        // Configure SessionLog
        modelBuilder.Entity<SessionLog>(entity =>
        {
            entity.HasKey(e => e.SessionLogId);
            entity.Property(e => e.SessionLogId).ValueGeneratedOnAdd();

            entity.Property(e => e.SessionToken)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.IpAddress)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.UserAgent)
                .HasMaxLength(500);

            entity.Property(e => e.DeviceInfo)
                .HasMaxLength(500);

            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.Property(e => e.TerminationReason)
                .HasMaxLength(256);

            entity.Property(e => e.LoginTime)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();

            entity.HasIndex(e => e.SessionToken).IsUnique();
            entity.HasIndex(e => e.UserId);
        });

        // Configure AuditLog
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId);
            entity.Property(e => e.AuditLogId).ValueGeneratedOnAdd();

            entity.Property(e => e.AuditAction)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.TableName)
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(e => e.RecordId)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.OldValues)
                .HasColumnType("nvarchar(max)");

            entity.Property(e => e.NewValues)
                .HasColumnType("nvarchar(max)");

            entity.Property(e => e.ChangeDescription)
                .HasMaxLength(500);

            entity.Property(e => e.IpAddress)
                .HasMaxLength(50);

            entity.Property(e => e.SessionId)
                .HasMaxLength(256);

            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();

            entity.HasIndex(e => new { e.TableName, e.CreatedDate });
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedDate);
        });

    }
}
