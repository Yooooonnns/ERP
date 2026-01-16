using DigitalisationERP.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DigitalisationERP.API.Services;

internal static class SqliteSchemaBootstrapper
{
    public static void EnsureStockDiagramSchema(ApplicationDbContext db)
    {
        if (db.Database.ProviderName == null || !db.Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // EnsureCreated does not evolve schema. For dev purposes, ensure new tables exist.
        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ProductionLineDefinitions (
    Id INTEGER NOT NULL CONSTRAINT PK_ProductionLineDefinitions PRIMARY KEY AUTOINCREMENT,
    LineId TEXT NOT NULL,
    LineName TEXT NOT NULL,
    Description TEXT NULL,
    IsActive INTEGER NOT NULL,
    OutputMaterialNumber TEXT NOT NULL,
    CreatedBy TEXT NULL,
    CreatedOn TEXT NOT NULL,
    ChangedBy TEXT NULL,
    ChangedOn TEXT NULL
);
");

        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ProductionLineInputs (
    Id INTEGER NOT NULL CONSTRAINT PK_ProductionLineInputs PRIMARY KEY AUTOINCREMENT,
    ProductionLineDefinitionId INTEGER NOT NULL,
    MaterialNumber TEXT NOT NULL,
    QuantityPerUnit TEXT NOT NULL,
    UnitOfMeasure TEXT NOT NULL,
    CreatedBy TEXT NULL,
    CreatedOn TEXT NOT NULL,
    ChangedBy TEXT NULL,
    ChangedOn TEXT NULL,
    CONSTRAINT FK_ProductionLineInputs_Line FOREIGN KEY (ProductionLineDefinitionId) REFERENCES ProductionLineDefinitions (Id) ON DELETE CASCADE
);
");

        // Optional FK to materials by MaterialNumber (we enforce uniqueness).
        // Some existing databases may not have MaterialNumber unique, so this is best-effort via index.
        db.Database.ExecuteSqlRaw(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_Materials_MaterialNumber ON Materials (MaterialNumber);");
        db.Database.ExecuteSqlRaw(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_ProductionLineDefinitions_LineId ON ProductionLineDefinitions (LineId);");
        db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_ProductionLineInputs_LineId ON ProductionLineInputs (ProductionLineDefinitionId);");
        db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_ProductionLineInputs_MaterialNumber ON ProductionLineInputs (MaterialNumber);");
    }
}
