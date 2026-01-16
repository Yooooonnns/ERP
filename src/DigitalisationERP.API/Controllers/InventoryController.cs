using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigitalisationERP.Core.Entities;
using DigitalisationERP.Infrastructure.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DigitalisationERP.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public InventoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/inventory/items
        [HttpGet("items")]
        public async Task<ActionResult<IEnumerable<object>>> GetInventoryItems(
            [FromQuery] string search = "",
            [FromQuery] bool? lowStock = null)
        {
            var query = _context.InventoryItems.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(i => i.ItemCode.Contains(search) || i.ItemName.Contains(search));
            }

            if (lowStock == true)
            {
                query = query.Where(i => i.CurrentQuantity <= i.ReorderPoint);
            }

            var items = await query
                .Select(i => new
                {
                    i.Id,
                    i.ItemCode,
                    ItemName = i.ItemName,
                    i.CurrentQuantity,
                    i.MinimumQuantity,
                    i.ReorderPoint,
                    i.ReorderQuantity,
                    i.MaximumQuantity,
                    i.UnitCost,
                    i.TotalValue,
                    Unit = i.Unit,
                    i.WarehouseLocation,
                    i.BinLocation,
                    i.RequiresLotTracking,
                    i.RequiresSerialTracking,
                    i.FifoFefoPolicy,
                    NeedsReorder = i.CurrentQuantity <= i.ReorderPoint,
                    StockStatus = i.CurrentQuantity <= i.MinimumQuantity ? "Critical" :
                                  i.CurrentQuantity <= i.ReorderPoint ? "Low" :
                                  i.CurrentQuantity >= i.MaximumQuantity ? "Overstocked" : "Normal"
                })
                .ToListAsync();

            return Ok(items);
        }

        // GET: api/inventory/items/{id}
        [HttpGet("items/{id}")]
        public async Task<ActionResult<object>> GetInventoryItem(long id)
        {
            var item = await _context.InventoryItems
                .Where(i => i.Id == id)
                .Select(i => new
                {
                    i.Id,
                    i.ItemCode,
                    ItemName = i.ItemName,
                    i.CurrentQuantity,
                    i.MinimumQuantity,
                    i.ReorderPoint,
                    i.ReorderQuantity,
                    i.MaximumQuantity,
                    i.UnitCost,
                    i.TotalValue,
                    Unit = i.Unit,
                    i.WarehouseLocation,
                    i.BinLocation,
                    i.RequiresLotTracking,
                    i.RequiresSerialTracking,
                    i.FifoFefoPolicy,
                    i.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (item == null)
            {
                return NotFound(new { message = "Inventory item not found" });
            }

            return Ok(item);
        }

        // POST: api/inventory/items
        [HttpPost("items")]
        public async Task<ActionResult<InventoryItem>> CreateInventoryItem(InventoryItem item)
        {
            if (await _context.InventoryItems.AnyAsync(i => i.ItemCode == item.ItemCode))
            {
                return BadRequest(new { message = "Item code already exists" });
            }

            item.TotalValue = item.CurrentQuantity * item.UnitCost;
            item.CreatedAt = DateTime.UtcNow;

            _context.InventoryItems.Add(item);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetInventoryItem), new { id = item.Id }, item);
        }

        // PUT: api/inventory/items/{id}
        [HttpPut("items/{id}")]
        public async Task<IActionResult> UpdateInventoryItem(long id, InventoryItem item)
        {
            if (id != item.Id)
            {
                return BadRequest(new { message = "ID mismatch" });
            }

            var existingItem = await _context.InventoryItems.FindAsync(id);
            if (existingItem == null)
            {
                return NotFound(new { message = "Inventory item not found" });
            }

            existingItem.ItemName = item.ItemName;
            existingItem.MinimumQuantity = item.MinimumQuantity;
            existingItem.ReorderPoint = item.ReorderPoint;
            existingItem.ReorderQuantity = item.ReorderQuantity;
            existingItem.MaximumQuantity = item.MaximumQuantity;
            existingItem.UnitCost = item.UnitCost;
            existingItem.WarehouseLocation = item.WarehouseLocation;
            existingItem.BinLocation = item.BinLocation;
            existingItem.RequiresLotTracking = item.RequiresLotTracking;
            existingItem.RequiresSerialTracking = item.RequiresSerialTracking;
            existingItem.FifoFefoPolicy = item.FifoFefoPolicy;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/inventory/items/{id}/transactions
        [HttpGet("items/{id}/transactions")]
        public async Task<ActionResult<IEnumerable<object>>> GetItemTransactions(long id)
        {
            var transactions = await _context.InventoryTransactions
                .Where(t => t.InventoryItemId == id)
                .OrderByDescending(t => t.TransactionDate)
                .Select(t => new
                {
                    t.Id,
                    t.TransactionType,
                    t.Quantity,
                    t.UnitCost,
                    t.TotalCost,
                    t.TransactionDate,
                    t.Notes,
                    t.LotBatchId,
                    t.ProductionPostId,
                    t.ProductionTaskId,
                    t.UserId,
                    t.UserName
                })
                .ToListAsync();

            return Ok(transactions);
        }

        // POST: api/inventory/transactions
        [HttpPost("transactions")]
        public async Task<ActionResult<InventoryTransaction>> CreateTransaction(InventoryTransaction transaction)
        {
            var item = await _context.InventoryItems.FindAsync(transaction.InventoryItemId);
            if (item == null)
            {
                return BadRequest(new { message = "Inventory item not found" });
            }

            // Update inventory quantity based on transaction type
            switch (transaction.TransactionType)
            {
                case InventoryTransactionTypeEnum.Receipt:
                case InventoryTransactionTypeEnum.Return:
                case InventoryTransactionTypeEnum.Restock:
                    item.CurrentQuantity += transaction.Quantity;
                    break;
                case InventoryTransactionTypeEnum.Consumption:
                case InventoryTransactionTypeEnum.Transfer:
                case InventoryTransactionTypeEnum.Scrap:
                    item.CurrentQuantity -= transaction.Quantity;
                    break;
                case InventoryTransactionTypeEnum.Adjustment:
                    item.CurrentQuantity = transaction.Quantity; // Adjustment sets the quantity
                    break;
            }

            item.TotalValue = item.CurrentQuantity * item.UnitCost;

            transaction.TotalCost = transaction.Quantity * transaction.UnitCost;
            transaction.TransactionDate = DateTime.UtcNow;

            _context.InventoryTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetItemTransactions), new { id = transaction.InventoryItemId }, transaction);
        }

        // GET: api/inventory/low-stock
        [HttpGet("low-stock")]
        public async Task<ActionResult<IEnumerable<object>>> GetLowStockItems()
        {
            var items = await _context.InventoryItems
                .Where(i => i.CurrentQuantity <= i.ReorderPoint)
                .OrderBy(i => i.CurrentQuantity)
                .Select(i => new
                {
                    i.Id,
                    i.ItemCode,
                    ItemName = i.ItemName,
                    i.CurrentQuantity,
                    i.ReorderPoint,
                    i.ReorderQuantity,
                    Deficit = i.ReorderPoint - i.CurrentQuantity,
                    RecommendedOrderQuantity = i.ReorderQuantity,
                    EstimatedCost = i.ReorderQuantity * i.UnitCost
                })
                .ToListAsync();

            return Ok(items);
        }

        // POST: api/inventory/reorder
        [HttpPost("reorder")]
        public async Task<ActionResult<object>> TriggerReorder([FromBody] List<long> itemIds)
        {
            var items = await _context.InventoryItems
                .Where(i => itemIds.Contains(i.Id))
                .ToListAsync();

            var reorderRequests = new List<object>();

            foreach (var item in items)
            {
                if (item.CurrentQuantity <= item.ReorderPoint)
                {
                    // Create a transaction for the reorder
                    var transaction = new InventoryTransaction
                    {
                        InventoryItemId = item.Id,
                        TransactionType = InventoryTransactionTypeEnum.Reservation,
                        Quantity = item.ReorderQuantity,
                        UnitCost = item.UnitCost,
                        TotalCost = item.ReorderQuantity * item.UnitCost,
                        TransactionDate = DateTime.UtcNow,
                        Notes = "Automatic reorder triggered"
                    };

                    _context.InventoryTransactions.Add(transaction);

                    reorderRequests.Add(new
                    {
                        item.ItemCode,
                        ItemName = item.ItemName,
                        OrderQuantity = item.ReorderQuantity,
                        EstimatedCost = item.ReorderQuantity * item.UnitCost,
                        Status = "Pending"
                    });
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { 
                message = $"Reorder initiated for {reorderRequests.Count} items",
                requests = reorderRequests
            });
        }

        // GET: api/inventory/lots
        [HttpGet("lots")]
        public async Task<ActionResult<IEnumerable<object>>> GetLotBatches(
            [FromQuery] long? itemId = null,
            [FromQuery] LotBatchStatusEnum? status = null)
        {
            var query = _context.LotBatches.AsQueryable();

            if (itemId.HasValue)
            {
                query = query.Where(l => l.InventoryItemId == itemId.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(l => l.Status == status.Value);
            }

            var lots = await query
                .Include(l => l.InventoryItem)
                .Select(l => new
                {
                    l.Id,
                    l.LotNumber,
                    l.BatchNumber,
                    l.Quantity,
                    l.RemainingQuantity,
                    l.ReceivedDate,
                    l.ManufactureDate,
                    l.ExpiryDate,
                    l.Status,
                    l.QualityApproved,
                    l.SupplierName,
                    ItemCode = l.InventoryItem.ItemCode,
                    ItemName = l.InventoryItem.ItemName,
                    DaysUntilExpiry = l.ExpiryDate.HasValue ? (l.ExpiryDate.Value - DateTime.UtcNow).Days : (int?)null
                })
                .OrderBy(l => l.ExpiryDate)
                .ToListAsync();

            return Ok(lots);
        }

        // GET: api/inventory/statistics
        [HttpGet("statistics")]
        public async Task<ActionResult<object>> GetInventoryStatistics()
        {
            var totalItems = await _context.InventoryItems.CountAsync();
            var lowStockItems = await _context.InventoryItems.CountAsync(i => i.CurrentQuantity <= i.ReorderPoint);
            var criticalItems = await _context.InventoryItems.CountAsync(i => i.CurrentQuantity <= i.MinimumQuantity);
            var totalValue = await _context.InventoryItems.SumAsync(i => i.TotalValue);

            var recentTransactions = await _context.InventoryTransactions
                .Where(t => t.TransactionDate >= DateTime.UtcNow.AddDays(-7))
                .CountAsync();

            var expiringLots = await _context.LotBatches
                .CountAsync(l => l.ExpiryDate.HasValue && l.ExpiryDate.Value <= DateTime.UtcNow.AddDays(30));

            return Ok(new
            {
                totalItems,
                lowStockItems,
                criticalItems,
                totalValue,
                recentTransactions,
                expiringLots,
                generatedAt = DateTime.UtcNow
            });
        }
    }
}
