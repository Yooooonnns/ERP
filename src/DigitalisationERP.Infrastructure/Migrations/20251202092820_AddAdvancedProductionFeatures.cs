using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DigitalisationERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvancedProductionFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DashboardWidgets",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    DashboardName = table.Column<string>(type: "text", nullable: false),
                    WidgetType = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    PositionX = table.Column<int>(type: "integer", nullable: false),
                    PositionY = table.Column<int>(type: "integer", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "text", nullable: false),
                    DataSourceQuery = table.Column<string>(type: "text", nullable: true),
                    RefreshIntervalSeconds = table.Column<int>(type: "integer", nullable: true),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardWidgets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Emails",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SenderId = table.Column<long>(type: "bigint", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDraft = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Emails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Emails_Users_SenderId",
                        column: x => x.SenderId,
                        principalSchema: "erp",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemCode = table.Column<string>(type: "text", nullable: false),
                    ItemName = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    CurrentQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    MinimumQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    ReorderPoint = table.Column<decimal>(type: "numeric", nullable: false),
                    ReorderQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    MaximumQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalValue = table.Column<decimal>(type: "numeric", nullable: false),
                    WarehouseLocation = table.Column<string>(type: "text", nullable: true),
                    BinLocation = table.Column<string>(type: "text", nullable: true),
                    RequiresLotTracking = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresSerialTracking = table.Column<bool>(type: "boolean", nullable: false),
                    FifoFefoPolicy = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastRestockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductionPosts",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    SequenceOrder = table.Column<int>(type: "integer", nullable: false),
                    Department = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    CurrentLoad = table.Column<int>(type: "integer", nullable: false),
                    MinRawMaterialLevel = table.Column<decimal>(type: "numeric", nullable: false),
                    CurrentRawMaterialLevel = table.Column<decimal>(type: "numeric", nullable: false),
                    ClientId = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedBy = table.Column<string>(type: "text", nullable: true),
                    ChangedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionPosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShiftHandovers",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HandoverNumber = table.Column<string>(type: "text", nullable: false),
                    ShiftDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Shift = table.Column<int>(type: "integer", nullable: false),
                    OutgoingUserId = table.Column<string>(type: "text", nullable: false),
                    OutgoingUserName = table.Column<string>(type: "text", nullable: false),
                    IncomingUserId = table.Column<string>(type: "text", nullable: true),
                    IncomingUserName = table.Column<string>(type: "text", nullable: true),
                    TargetQuantity = table.Column<int>(type: "integer", nullable: false),
                    ActualQuantity = table.Column<int>(type: "integer", nullable: false),
                    QualityRejects = table.Column<int>(type: "integer", nullable: false),
                    ProductionStatus = table.Column<string>(type: "text", nullable: false),
                    OutstandingIssues = table.Column<string>(type: "text", nullable: true),
                    MaterialStatus = table.Column<string>(type: "text", nullable: true),
                    EquipmentStatus = table.Column<string>(type: "text", nullable: true),
                    GeneralNotes = table.Column<string>(type: "text", nullable: false),
                    SafetyNotes = table.Column<string>(type: "text", nullable: true),
                    QualityNotes = table.Column<string>(type: "text", nullable: true),
                    MaintenanceNotes = table.Column<string>(type: "text", nullable: true),
                    PendingTasks = table.Column<string>(type: "text", nullable: true),
                    ActiveProductionPostIds = table.Column<string>(type: "text", nullable: true),
                    OpenIssueIds = table.Column<string>(type: "text", nullable: true),
                    AcknowledgedByIncoming = table.Column<bool>(type: "boolean", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftHandovers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailAttachments",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmailId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    FilePath = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailAttachments_Emails_EmailId",
                        column: x => x.EmailId,
                        principalSchema: "erp",
                        principalTable: "Emails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailRecipients",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmailId = table.Column<int>(type: "integer", nullable: false),
                    RecipientId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailRecipients_Emails_EmailId",
                        column: x => x.EmailId,
                        principalSchema: "erp",
                        principalTable: "Emails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmailRecipients_Users_RecipientId",
                        column: x => x.RecipientId,
                        principalSchema: "erp",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LotBatches",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InventoryItemId = table.Column<int>(type: "integer", nullable: false),
                    LotNumber = table.Column<string>(type: "text", nullable: false),
                    BatchNumber = table.Column<string>(type: "text", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    ReceivedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ManufactureDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    QualityApproved = table.Column<bool>(type: "boolean", nullable: false),
                    QualityNotes = table.Column<string>(type: "text", nullable: true),
                    SupplierName = table.Column<string>(type: "text", nullable: true),
                    SupplierLotNumber = table.Column<string>(type: "text", nullable: true),
                    WarehouseLocation = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotBatches_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalSchema: "erp",
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceSchedules",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductionPostId = table.Column<long>(type: "bigint", nullable: false),
                    MaintenanceCode = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    MaintenanceType = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EstimatedDurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    ActualDurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    TriggerUsageHours = table.Column<int>(type: "integer", nullable: true),
                    TriggerCycleCount = table.Column<int>(type: "integer", nullable: true),
                    LastMaintenanceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CurrentUsageHours = table.Column<int>(type: "integer", nullable: true),
                    CurrentCycleCount = table.Column<int>(type: "integer", nullable: true),
                    HealthScore = table.Column<double>(type: "double precision", nullable: true),
                    IsRecurring = table.Column<bool>(type: "boolean", nullable: false),
                    RecurrenceIntervalDays = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssignedToUserId = table.Column<string>(type: "text", nullable: true),
                    AssignedToUserName = table.Column<string>(type: "text", nullable: true),
                    RequiredParts = table.Column<string>(type: "text", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "numeric", nullable: true),
                    ActualCost = table.Column<decimal>(type: "numeric", nullable: true),
                    CompletionNotes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceSchedules_ProductionPosts_ProductionPostId",
                        column: x => x.ProductionPostId,
                        principalSchema: "erp",
                        principalTable: "ProductionPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaterialRequests",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestNumber = table.Column<string>(type: "text", nullable: false),
                    ProductionPostId = table.Column<long>(type: "bigint", nullable: false),
                    RequestedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    MaterialName = table.Column<string>(type: "text", nullable: false),
                    RequestedQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FulfilledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FulfilledByUserId = table.Column<long>(type: "bigint", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    BotNotified = table.Column<bool>(type: "boolean", nullable: false),
                    BotNotifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClientId = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedBy = table.Column<string>(type: "text", nullable: true),
                    ChangedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialRequests_ProductionPosts_ProductionPostId",
                        column: x => x.ProductionPostId,
                        principalSchema: "erp",
                        principalTable: "ProductionPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialRequests_Users_FulfilledByUserId",
                        column: x => x.FulfilledByUserId,
                        principalSchema: "erp",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MaterialRequests_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "erp",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionSchedules",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScheduleNumber = table.Column<string>(type: "text", nullable: false),
                    ProductionOrderId = table.Column<long>(type: "bigint", nullable: false),
                    PlannedStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PlannedEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActualStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PlannedDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    ActualDurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    AssignedProductionPostId = table.Column<long>(type: "bigint", nullable: true),
                    AssignedOperatorIds = table.Column<string>(type: "text", nullable: true),
                    SetupTimeMinutes = table.Column<int>(type: "integer", nullable: true),
                    SetupRequirements = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MaterialConstraints = table.Column<string>(type: "text", nullable: true),
                    ToolConstraints = table.Column<string>(type: "text", nullable: true),
                    SkillConstraints = table.Column<string>(type: "text", nullable: true),
                    PredecessorScheduleId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionSchedules_ProductionPosts_AssignedProductionPostId",
                        column: x => x.AssignedProductionPostId,
                        principalSchema: "erp",
                        principalTable: "ProductionPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionSchedules_ProductionSchedules_PredecessorSchedule~",
                        column: x => x.PredecessorScheduleId,
                        principalSchema: "erp",
                        principalTable: "ProductionSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionTasks",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaskNumber = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ProductionOrderId = table.Column<long>(type: "bigint", nullable: false),
                    AssignedPostId = table.Column<long>(type: "bigint", nullable: true),
                    AssignedUserId = table.Column<long>(type: "bigint", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EstimatedHours = table.Column<int>(type: "integer", nullable: false),
                    ActualHours = table.Column<int>(type: "integer", nullable: false),
                    RequiredMaterials = table.Column<string>(type: "text", nullable: false),
                    MaterialsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    QualityCheckPassed = table.Column<bool>(type: "boolean", nullable: false),
                    QualityNotes = table.Column<string>(type: "text", nullable: true),
                    ClientId = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedBy = table.Column<string>(type: "text", nullable: true),
                    ChangedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionTasks_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalSchema: "erp",
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionTasks_ProductionPosts_AssignedPostId",
                        column: x => x.AssignedPostId,
                        principalSchema: "erp",
                        principalTable: "ProductionPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionTasks_Users_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalSchema: "erp",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceHistories",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaintenanceScheduleId = table.Column<int>(type: "integer", nullable: false),
                    ExecutionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    PerformedByUserId = table.Column<string>(type: "text", nullable: false),
                    PerformedByUserName = table.Column<string>(type: "text", nullable: false),
                    WorkPerformed = table.Column<string>(type: "text", nullable: false),
                    PartsReplaced = table.Column<string>(type: "text", nullable: true),
                    Cost = table.Column<decimal>(type: "numeric", nullable: false),
                    IssuesFound = table.Column<string>(type: "text", nullable: true),
                    RecommendedActions = table.Column<string>(type: "text", nullable: true),
                    EquipmentFunctionalAfter = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceHistories_MaintenanceSchedules_MaintenanceSchedu~",
                        column: x => x.MaintenanceScheduleId,
                        principalSchema: "erp",
                        principalTable: "MaintenanceSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransactions",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InventoryItemId = table.Column<int>(type: "integer", nullable: false),
                    TransactionNumber = table.Column<string>(type: "text", nullable: false),
                    TransactionType = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalCost = table.Column<decimal>(type: "numeric", nullable: false),
                    LotBatchId = table.Column<int>(type: "integer", nullable: true),
                    ProductionPostId = table.Column<long>(type: "bigint", nullable: true),
                    ProductionTaskId = table.Column<long>(type: "bigint", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    UserName = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalSchema: "erp",
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_LotBatches_LotBatchId",
                        column: x => x.LotBatchId,
                        principalSchema: "erp",
                        principalTable: "LotBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_ProductionPosts_ProductionPostId",
                        column: x => x.ProductionPostId,
                        principalSchema: "erp",
                        principalTable: "ProductionPosts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_ProductionTasks_ProductionTaskId",
                        column: x => x.ProductionTaskId,
                        principalSchema: "erp",
                        principalTable: "ProductionTasks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "IssueEscalations",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IssueNumber = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ProductionPostId = table.Column<long>(type: "bigint", nullable: true),
                    ProductionTaskId = table.Column<long>(type: "bigint", nullable: true),
                    IssueType = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ReportedByUserId = table.Column<string>(type: "text", nullable: false),
                    ReportedByUserName = table.Column<string>(type: "text", nullable: false),
                    AssignedToUserId = table.Column<string>(type: "text", nullable: true),
                    AssignedToUserName = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "text", nullable: true),
                    RootCause = table.Column<string>(type: "text", nullable: true),
                    CorrectiveAction = table.Column<string>(type: "text", nullable: true),
                    DowntimeMinutes = table.Column<int>(type: "integer", nullable: true),
                    CostImpact = table.Column<decimal>(type: "numeric", nullable: true),
                    EscalationLevel = table.Column<int>(type: "integer", nullable: false),
                    LastEscalatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttachmentUrls = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueEscalations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssueEscalations_ProductionPosts_ProductionPostId",
                        column: x => x.ProductionPostId,
                        principalSchema: "erp",
                        principalTable: "ProductionPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_IssueEscalations_ProductionTasks_ProductionTaskId",
                        column: x => x.ProductionTaskId,
                        principalSchema: "erp",
                        principalTable: "ProductionTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProductionChatMessages",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageText = table.Column<string>(type: "text", nullable: false),
                    SenderUserId = table.Column<string>(type: "text", nullable: false),
                    SenderUserName = table.Column<string>(type: "text", nullable: false),
                    ProductionPostId = table.Column<long>(type: "bigint", nullable: true),
                    ProductionTaskId = table.Column<long>(type: "bigint", nullable: true),
                    IssueEscalationId = table.Column<int>(type: "integer", nullable: true),
                    MessageType = table.Column<int>(type: "integer", nullable: false),
                    AttachmentUrls = table.Column<string>(type: "text", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ParentMessageId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionChatMessages_IssueEscalations_IssueEscalationId",
                        column: x => x.IssueEscalationId,
                        principalSchema: "erp",
                        principalTable: "IssueEscalations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductionChatMessages_ProductionChatMessages_ParentMessage~",
                        column: x => x.ParentMessageId,
                        principalSchema: "erp",
                        principalTable: "ProductionChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionChatMessages_ProductionPosts_ProductionPostId",
                        column: x => x.ProductionPostId,
                        principalSchema: "erp",
                        principalTable: "ProductionPosts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductionChatMessages_ProductionTasks_ProductionTaskId",
                        column: x => x.ProductionTaskId,
                        principalSchema: "erp",
                        principalTable: "ProductionTasks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailAttachments_EmailId",
                schema: "erp",
                table: "EmailAttachments",
                column: "EmailId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailRecipients_EmailId",
                schema: "erp",
                table: "EmailRecipients",
                column: "EmailId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailRecipients_RecipientId",
                schema: "erp",
                table: "EmailRecipients",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_Emails_SenderId",
                schema: "erp",
                table: "Emails",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_ItemCode",
                schema: "erp",
                table: "InventoryItems",
                column: "ItemCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_InventoryItemId",
                schema: "erp",
                table: "InventoryTransactions",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_LotBatchId",
                schema: "erp",
                table: "InventoryTransactions",
                column: "LotBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ProductionPostId",
                schema: "erp",
                table: "InventoryTransactions",
                column: "ProductionPostId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ProductionTaskId",
                schema: "erp",
                table: "InventoryTransactions",
                column: "ProductionTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueEscalations_ProductionPostId",
                schema: "erp",
                table: "IssueEscalations",
                column: "ProductionPostId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueEscalations_ProductionTaskId",
                schema: "erp",
                table: "IssueEscalations",
                column: "ProductionTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_LotBatches_InventoryItemId",
                schema: "erp",
                table: "LotBatches",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceHistories_MaintenanceScheduleId",
                schema: "erp",
                table: "MaintenanceHistories",
                column: "MaintenanceScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_ProductionPostId",
                schema: "erp",
                table: "MaintenanceSchedules",
                column: "ProductionPostId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequests_FulfilledByUserId",
                schema: "erp",
                table: "MaterialRequests",
                column: "FulfilledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequests_ProductionPostId",
                schema: "erp",
                table: "MaterialRequests",
                column: "ProductionPostId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequests_RequestedByUserId",
                schema: "erp",
                table: "MaterialRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionChatMessages_IssueEscalationId",
                schema: "erp",
                table: "ProductionChatMessages",
                column: "IssueEscalationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionChatMessages_ParentMessageId",
                schema: "erp",
                table: "ProductionChatMessages",
                column: "ParentMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionChatMessages_ProductionPostId",
                schema: "erp",
                table: "ProductionChatMessages",
                column: "ProductionPostId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionChatMessages_ProductionTaskId",
                schema: "erp",
                table: "ProductionChatMessages",
                column: "ProductionTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPosts_Code",
                schema: "erp",
                table: "ProductionPosts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSchedules_AssignedProductionPostId",
                schema: "erp",
                table: "ProductionSchedules",
                column: "AssignedProductionPostId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSchedules_PredecessorScheduleId",
                schema: "erp",
                table: "ProductionSchedules",
                column: "PredecessorScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionTasks_AssignedPostId",
                schema: "erp",
                table: "ProductionTasks",
                column: "AssignedPostId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionTasks_AssignedUserId",
                schema: "erp",
                table: "ProductionTasks",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionTasks_ProductionOrderId",
                schema: "erp",
                table: "ProductionTasks",
                column: "ProductionOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DashboardWidgets",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "EmailAttachments",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "EmailRecipients",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "InventoryTransactions",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "MaintenanceHistories",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "MaterialRequests",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "ProductionChatMessages",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "ProductionSchedules",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "ShiftHandovers",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "Emails",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "LotBatches",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "MaintenanceSchedules",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "IssueEscalations",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "InventoryItems",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "ProductionTasks",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "ProductionPosts",
                schema: "erp");
        }
    }
}
