using DigitalisationERP.Core.Entities.MM;
using DigitalisationERP.Core.Enums;
using DigitalisationERP.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DigitalisationERP.API.Controllers;

[ApiController]
[Route("api/materials")]
[Authorize]
public class MaterialsController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MaterialDto>>> GetAll([FromQuery] MaterialType? type = null)
    {
        var query = db.Materials.AsNoTracking();
        if (type != null)
        {
            query = query.Where(m => m.MaterialType == type);
        }

        var items = await query
            .OrderBy(m => m.MaterialNumber)
            .Select(m => new MaterialDto(
                m.MaterialNumber,
                m.Description,
                m.MaterialType,
                m.BaseUnitOfMeasure,
                m.StockQuantity,
                m.MinimumStock,
                m.MaximumStock))
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{materialNumber}")]
    public async Task<ActionResult<MaterialDto>> GetByNumber(string materialNumber)
    {
        var mn = (materialNumber ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(mn)) return BadRequest("MaterialNumber is required.");

        var material = await db.Materials.AsNoTracking().FirstOrDefaultAsync(m => m.MaterialNumber == mn);
        if (material == null) return NotFound();

        return Ok(new MaterialDto(
            material.MaterialNumber,
            material.Description,
            material.MaterialType,
            material.BaseUnitOfMeasure,
            material.StockQuantity,
            material.MinimumStock,
            material.MaximumStock));
    }

    [HttpGet("{materialNumber}/movements")]
    public async Task<ActionResult<List<StockMovementDto>>> GetMovements(string materialNumber, [FromQuery] int take = 200)
    {
        var mn = (materialNumber ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(mn)) return BadRequest("MaterialNumber is required.");

        take = Math.Clamp(take, 1, 2000);

        var movements = await db.StockMovements.AsNoTracking()
            .Where(m => m.MaterialNumber == mn)
            .OrderByDescending(m => m.PostingDate)
            .Take(take)
            .Select(m => new StockMovementDto(
                m.DocumentNumber,
                m.MovementType,
                m.MaterialNumber,
                m.Quantity,
                m.UnitOfMeasure,
                m.ProductionOrderNumber,
                m.PostingDate,
                m.Status))
            .ToListAsync();

        return Ok(movements);
    }

    [HttpPost]
    public async Task<ActionResult<MaterialDto>> Create(MaterialUpsertRequest request)
    {
        if (request == null) return BadRequest();

        var materialNumber = (request.MaterialNumber ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(materialNumber)) return BadRequest("MaterialNumber is required.");

        var uom = (request.UnitOfMeasure ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(uom)) uom = "PC";

        var existing = await db.Materials.FirstOrDefaultAsync(m => m.MaterialNumber == materialNumber);
        if (existing != null)
        {
            // Light upsert: update master data fields, keep stock as-is unless explicit.
            existing.Description = request.Description ?? existing.Description;
            existing.MaterialType = request.MaterialType;
            existing.BaseUnitOfMeasure = uom;
            existing.MinimumStock = request.MinimumStock;
            existing.MaximumStock = request.MaximumStock;

            if (request.InitialStockQuantity != null)
            {
                existing.StockQuantity = request.InitialStockQuantity.Value;
            }

            await db.SaveChangesAsync();

            return Ok(new MaterialDto(
                existing.MaterialNumber,
                existing.Description,
                existing.MaterialType,
                existing.BaseUnitOfMeasure,
                existing.StockQuantity,
                existing.MinimumStock,
                existing.MaximumStock));
        }

        var material = new Material
        {
            MaterialNumber = materialNumber,
            Description = request.Description ?? materialNumber,
            MaterialType = request.MaterialType,
            BaseUnitOfMeasure = uom,
            StockQuantity = request.InitialStockQuantity ?? 0,
            MinimumStock = request.MinimumStock,
            MaximumStock = request.MaximumStock,
            Status = RecordStatus.Active
        };

        db.Materials.Add(material);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetByNumber), new { materialNumber = material.MaterialNumber }, new MaterialDto(
            material.MaterialNumber,
            material.Description,
            material.MaterialType,
            material.BaseUnitOfMeasure,
            material.StockQuantity,
            material.MinimumStock,
            material.MaximumStock));
    }

    [HttpPost("{materialNumber}/receive")]
    public async Task<ActionResult<MaterialDto>> Receive(string materialNumber, ReceiveMaterialRequest request)
    {
        var mn = (materialNumber ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(mn)) return BadRequest("MaterialNumber is required.");
        if (request == null) return BadRequest();
        if (request.Quantity <= 0) return BadRequest("Quantity must be > 0.");

        var material = await db.Materials.FirstOrDefaultAsync(m => m.MaterialNumber == mn);
        if (material == null) return NotFound();

        material.StockQuantity += request.Quantity;

        var docNo = string.IsNullOrWhiteSpace(request.DocumentNumber)
            ? $"GR-{DateTime.UtcNow:yyyyMMdd-HHmmss}" 
            : request.DocumentNumber.Trim();

        db.StockMovements.Add(new StockMovement
        {
            DocumentNumber = docNo,
            MovementType = "101",
            MaterialNumber = material.MaterialNumber,
            Quantity = request.Quantity,
            UnitOfMeasure = material.BaseUnitOfMeasure,
            PostingDate = DateTime.UtcNow,
            DocumentDate = DateTime.UtcNow,
            Status = MovementStatus.Completed,
            RobotExecuted = false
        });

        await db.SaveChangesAsync();

        return Ok(new MaterialDto(
            material.MaterialNumber,
            material.Description,
            material.MaterialType,
            material.BaseUnitOfMeasure,
            material.StockQuantity,
            material.MinimumStock,
            material.MaximumStock));
    }
}

public record MaterialDto(
    string MaterialNumber,
    string Description,
    MaterialType MaterialType,
    string UnitOfMeasure,
    decimal StockQuantity,
    decimal MinimumStock,
    decimal MaximumStock);

public record StockMovementDto(
    string DocumentNumber,
    string MovementType,
    string MaterialNumber,
    decimal Quantity,
    string UnitOfMeasure,
    string? ProductionOrderNumber,
    DateTime PostingDate,
    MovementStatus Status);

public record MaterialUpsertRequest
{
    public string? MaterialNumber { get; init; }
    public string? Description { get; init; }
    public MaterialType MaterialType { get; init; }
    public string? UnitOfMeasure { get; init; }
    public decimal? InitialStockQuantity { get; init; }
    public decimal MinimumStock { get; init; }
    public decimal MaximumStock { get; init; }
}

public record ReceiveMaterialRequest
{
    public decimal Quantity { get; init; }
    public string? DocumentNumber { get; init; }
}
