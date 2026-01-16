using Microsoft.EntityFrameworkCore;
using DigitalisationERP.Core.Entities;
using DigitalisationERP.Core.Entities.Auth;
using DigitalisationERP.Core.Entities.MM;
using DigitalisationERP.Core.Entities.PP;
using DigitalisationERP.Core.Entities.PM;
using DigitalisationERP.Core.Entities.IoT;
using DigitalisationERP.Core.Entities.Robotics;
using DigitalisationERP.Core.Entities.System;

namespace DigitalisationERP.Infrastructure.Data;

/// <summary>
/// Main database context following SAP HANA-style structure
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Authentication & Authorization
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Authorization> Authorizations => Set<Authorization>();
    public DbSet<AuthorizationField> AuthorizationFields => Set<AuthorizationField>();
    public DbSet<RoleAuthorization> RoleAuthorizations => Set<RoleAuthorization>();
    public DbSet<UserActivity> UserActivities => Set<UserActivity>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();

    // System
    public DbSet<EmailQueue> EmailQueue => Set<EmailQueue>();
    
    // Email System
    public DbSet<DigitalisationERP.Core.Entities.Email> Emails => Set<DigitalisationERP.Core.Entities.Email>();
    public DbSet<EmailRecipient> EmailRecipients => Set<EmailRecipient>();
    public DbSet<EmailAttachment> EmailAttachments => Set<EmailAttachment>();
    
    // Production Management
    public DbSet<ProductionPost> ProductionPosts => Set<ProductionPost>();
    public DbSet<ProductionTask> ProductionTasks => Set<ProductionTask>();
    public DbSet<MaterialRequest> MaterialRequests => Set<MaterialRequest>();
    
    // Inventory Intelligence
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<LotBatch> LotBatches => Set<LotBatch>();
    
    // Maintenance Management
    public DbSet<MaintenanceSchedule> MaintenanceSchedules => Set<MaintenanceSchedule>();
    public DbSet<MaintenanceHistory> MaintenanceHistories => Set<MaintenanceHistory>();
    
    // Advanced Scheduling
    public DbSet<ProductionSchedule> ProductionSchedules => Set<ProductionSchedule>();
    
    // Communication & Collaboration
    public DbSet<ProductionChatMessage> ProductionChatMessages => Set<ProductionChatMessage>();
    public DbSet<IssueEscalation> IssueEscalations => Set<IssueEscalation>();
    public DbSet<ShiftHandover> ShiftHandovers => Set<ShiftHandover>();
    
    // Dashboards
    public DbSet<DashboardWidget> DashboardWidgets => Set<DashboardWidget>();

    // Materials Management
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    // Production Planning
    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();

    // Production Line Definitions (Stock Diagram)
    public DbSet<ProductionLineDefinition> ProductionLineDefinitions => Set<ProductionLineDefinition>();
    public DbSet<ProductionLineInput> ProductionLineInputs => Set<ProductionLineInput>();

    // Plant Maintenance
    public DbSet<MaintenanceOrder> MaintenanceOrders => Set<MaintenanceOrder>();

    // IoT & Sensors
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<SensorReading> SensorReadings => Set<SensorReading>();

    // Robotics
    public DbSet<RobotTask> RobotTasks => Set<RobotTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply SAP-style conventions
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Set default schema (like SAP schemas: SAPSR3, etc.)
            entityType.SetSchema("erp");
        }
        
        // Email System Configuration
        modelBuilder.Entity<DigitalisationERP.Core.Entities.Email>()
            .HasOne(e => e.Sender)
            .WithMany()
            .HasForeignKey(e => e.SenderId)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<EmailRecipient>()
            .HasOne(er => er.Email)
            .WithMany(e => e.Recipients)
            .HasForeignKey(er => er.EmailId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<EmailRecipient>()
            .HasOne(er => er.Recipient)
            .WithMany()
            .HasForeignKey(er => er.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<EmailAttachment>()
            .HasOne(ea => ea.Email)
            .WithMany(e => e.Attachments)
            .HasForeignKey(ea => ea.EmailId)
            .OnDelete(DeleteBehavior.Cascade);
            
        // Production Management
        modelBuilder.Entity<ProductionPost>()
            .HasIndex(pp => pp.Code)
            .IsUnique();
            
        modelBuilder.Entity<ProductionTask>()
            .HasOne(pt => pt.ProductionOrder)
            .WithMany(po => po.Tasks)
            .HasForeignKey(pt => pt.ProductionOrderId)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<ProductionTask>()
            .HasOne(pt => pt.AssignedPost)
            .WithMany(pp => pp.Tasks)
            .HasForeignKey(pt => pt.AssignedPostId)
            .OnDelete(DeleteBehavior.SetNull);
            
        modelBuilder.Entity<ProductionTask>()
            .HasOne(pt => pt.AssignedUser)
            .WithMany()
            .HasForeignKey(pt => pt.AssignedUserId)
            .OnDelete(DeleteBehavior.SetNull);
            
        modelBuilder.Entity<MaterialRequest>()
            .HasOne(mr => mr.ProductionPost)
            .WithMany(pp => pp.MaterialRequests)
            .HasForeignKey(mr => mr.ProductionPostId)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<MaterialRequest>()
            .HasOne(mr => mr.RequestedBy)
            .WithMany()
            .HasForeignKey(mr => mr.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<MaterialRequest>()
            .HasOne(mr => mr.FulfilledBy)
            .WithMany()
            .HasForeignKey(mr => mr.FulfilledByUserId)
            .OnDelete(DeleteBehavior.SetNull);
            
        // Inventory Intelligence
        modelBuilder.Entity<InventoryItem>()
            .HasIndex(i => i.ItemCode)
            .IsUnique();

        // Materials Management
        modelBuilder.Entity<Material>()
            .HasIndex(m => m.MaterialNumber)
            .IsUnique();

        // Production Line Definitions (Stock Diagram)
        modelBuilder.Entity<ProductionLineDefinition>()
            .HasIndex(l => l.LineId)
            .IsUnique();

        modelBuilder.Entity<ProductionLineDefinition>()
            .HasOne(l => l.OutputMaterial)
            .WithMany()
            .HasForeignKey(l => l.OutputMaterialNumber)
            .HasPrincipalKey(m => m.MaterialNumber)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductionLineInput>()
            .HasOne(i => i.ProductionLineDefinition)
            .WithMany(l => l.Inputs)
            .HasForeignKey(i => i.ProductionLineDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductionLineInput>()
            .HasOne(i => i.Material)
            .WithMany()
            .HasForeignKey(i => i.MaterialNumber)
            .HasPrincipalKey(m => m.MaterialNumber)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<InventoryTransaction>()
            .HasOne(it => it.InventoryItem)
            .WithMany(i => i.Transactions)
            .HasForeignKey(it => it.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<InventoryTransaction>()
            .HasOne(it => it.LotBatch)
            .WithMany(lb => lb.Transactions)
            .HasForeignKey(it => it.LotBatchId)
            .OnDelete(DeleteBehavior.SetNull);
            
        modelBuilder.Entity<LotBatch>()
            .HasOne(lb => lb.InventoryItem)
            .WithMany(i => i.LotBatches)
            .HasForeignKey(lb => lb.InventoryItemId)
            .OnDelete(DeleteBehavior.Cascade);
            
        // Maintenance Management
        modelBuilder.Entity<MaintenanceSchedule>()
            .HasOne(ms => ms.ProductionPost)
            .WithMany()
            .HasForeignKey(ms => ms.ProductionPostId)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<MaintenanceHistory>()
            .HasOne(mh => mh.MaintenanceSchedule)
            .WithMany(ms => ms.History)
            .HasForeignKey(mh => mh.MaintenanceScheduleId)
            .OnDelete(DeleteBehavior.Cascade);
            
        // Production Schedule
        modelBuilder.Entity<ProductionSchedule>()
            .HasOne(ps => ps.AssignedProductionPost)
            .WithMany()
            .HasForeignKey(ps => ps.AssignedProductionPostId)
            .OnDelete(DeleteBehavior.SetNull);
            
        modelBuilder.Entity<ProductionSchedule>()
            .HasOne(ps => ps.PredecessorSchedule)
            .WithMany(ps => ps.DependentSchedules)
            .HasForeignKey(ps => ps.PredecessorScheduleId)
            .OnDelete(DeleteBehavior.Restrict);
            
        // Communication
        modelBuilder.Entity<ProductionChatMessage>()
            .HasOne(pcm => pcm.ParentMessage)
            .WithMany(pcm => pcm.Replies)
            .HasForeignKey(pcm => pcm.ParentMessageId)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<IssueEscalation>()
            .HasOne(ie => ie.ProductionPost)
            .WithMany()
            .HasForeignKey(ie => ie.ProductionPostId)
            .OnDelete(DeleteBehavior.SetNull);
            
        modelBuilder.Entity<IssueEscalation>()
            .HasOne(ie => ie.ProductionTask)
            .WithMany()
            .HasForeignKey(ie => ie.ProductionTaskId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    /// <summary>
    /// Override SaveChanges to automatically set audit fields
    /// Similar to SAP's automatic timestamp and user tracking
    /// </summary>
    public override int SaveChanges()
    {
        UpdateAuditFields();
        return base.SaveChanges();
    }

    /// <summary>
    /// Override SaveChangesAsync to automatically set audit fields
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Automatically update audit fields (CreatedBy, CreatedOn, ChangedBy, ChangedOn)
    /// </summary>
    private void UpdateAuditFields()
    {
        var entries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedOn = DateTime.UtcNow;
                entry.Entity.CreatedBy = "SYSTEM"; // TODO: Get from current user context
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.ChangedOn = DateTime.UtcNow;
                entry.Entity.ChangedBy = "SYSTEM"; // TODO: Get from current user context
            }
        }
    }
}
