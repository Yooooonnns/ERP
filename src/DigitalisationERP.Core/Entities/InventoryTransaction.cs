using System;

namespace DigitalisationERP.Core.Entities;

public class InventoryTransaction
{
    public int Id { get; set; }
    public int InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    
    public string TransactionNumber { get; set; } = string.Empty;
    public InventoryTransactionTypeEnum TransactionType { get; set; }
    
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    
    // Lot/Batch reference
    public int? LotBatchId { get; set; }
    public LotBatch? LotBatch { get; set; }
    
    // Reference to production
    public long? ProductionPostId { get; set; }
    public ProductionPost? ProductionPost { get; set; }
    
    public long? ProductionTaskId { get; set; }
    public ProductionTask? ProductionTask { get; set; }
    
    // User tracking
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    
    public string? Notes { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
}

public enum InventoryTransactionTypeEnum
{
    Receipt,           // Material received
    Consumption,       // Used in production
    Transfer,          // Moved between locations
    Adjustment,        // Manual correction
    Return,            // Returned to supplier
    Scrap,             // Waste/damaged
    Restock,           // Added from bot delivery
    Reservation        // Reserved for order
}
