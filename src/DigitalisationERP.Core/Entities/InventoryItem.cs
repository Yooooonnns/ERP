using System;
using System.Collections.Generic;

namespace DigitalisationERP.Core.Entities;

public class InventoryItem
{
    public int Id { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    
    // Quantity tracking
    public decimal CurrentQuantity { get; set; }
    public decimal MinimumQuantity { get; set; }
    public decimal ReorderPoint { get; set; }
    public decimal ReorderQuantity { get; set; }
    public decimal MaximumQuantity { get; set; }
    
    // Cost tracking
    public decimal UnitCost { get; set; }
    public decimal TotalValue { get; set; }
    
    // Location
    public string? WarehouseLocation { get; set; }
    public string? BinLocation { get; set; }
    
    // Lot/Batch tracking
    public bool RequiresLotTracking { get; set; }
    public bool RequiresSerialTracking { get; set; }
    
    // FIFO/FEFO
    public FifoFefoPolicyEnum FifoFefoPolicy { get; set; }
    
    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastRestockedAt { get; set; }
    public DateTime? LastConsumedAt { get; set; }
    
    // Relationships
    public ICollection<InventoryTransaction> Transactions { get; set; } = new List<InventoryTransaction>();
    public ICollection<LotBatch> LotBatches { get; set; } = new List<LotBatch>();
}

public enum FifoFefoPolicyEnum
{
    None,
    FIFO,  // First In First Out
    FEFO,  // First Expired First Out
    LIFO   // Last In First Out
}
