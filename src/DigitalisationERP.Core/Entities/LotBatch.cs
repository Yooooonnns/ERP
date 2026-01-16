using System;
using System.Collections.Generic;

namespace DigitalisationERP.Core.Entities;

public class LotBatch
{
    public int Id { get; set; }
    public int InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    
    public string LotNumber { get; set; } = string.Empty;
    public string? BatchNumber { get; set; }
    
    public decimal Quantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    
    // Dates
    public DateTime ReceivedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ManufactureDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    
    // Status
    public LotBatchStatusEnum Status { get; set; }
    
    // Quality
    public bool QualityApproved { get; set; }
    public string? QualityNotes { get; set; }
    
    // Supplier info
    public string? SupplierName { get; set; }
    public string? SupplierLotNumber { get; set; }
    
    // Location
    public string? WarehouseLocation { get; set; }
    
    public ICollection<InventoryTransaction> Transactions { get; set; } = new List<InventoryTransaction>();
}

public enum LotBatchStatusEnum
{
    Active,
    Quarantine,
    Approved,
    Rejected,
    Depleted,
    Expired
}
