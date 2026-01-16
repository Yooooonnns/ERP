using DigitalisationERP.Core.Entities.MM;
using DigitalisationERP.Core.Entities.PP;
using DigitalisationERP.Core.Enums;
using DigitalisationERP.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DigitalisationERP.API.Controllers;

[ApiController]
[Route("api/production-lines")]
[Authorize]
public class ProductionLinesController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProductionLineDto>>> GetAll()
    {
        var lines = await db.ProductionLineDefinitions.AsNoTracking()
            .Include(l => l.Inputs)
            .OrderBy(l => l.LineId)
            .ToListAsync();

        var materialNumbers = lines
            .SelectMany(l => l.Inputs.Select(i => i.MaterialNumber).Append(l.OutputMaterialNumber))
            .Distinct()
            .ToList();

        var materials = await db.Materials.AsNoTracking()
            .Where(m => materialNumbers.Contains(m.MaterialNumber))
            .ToDictionaryAsync(m => m.MaterialNumber);

        return Ok(lines.Select(l => MapLine(l, materials)).ToList());
    }

    [HttpGet("{lineId}")]
    public async Task<ActionResult<ProductionLineDto>> GetOne(string lineId)
    {
        var id = (lineId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(id)) return BadRequest("LineId is required.");

        var line = await db.ProductionLineDefinitions.AsNoTracking()
            .Include(l => l.Inputs)
            .FirstOrDefaultAsync(l => l.LineId == id);

        if (line == null) return NotFound();

        var materialNumbers = line.Inputs.Select(i => i.MaterialNumber).Append(line.OutputMaterialNumber).Distinct().ToList();
        var materials = await db.Materials.AsNoTracking()
            .Where(m => materialNumbers.Contains(m.MaterialNumber))
            .ToDictionaryAsync(m => m.MaterialNumber);

        return Ok(MapLine(line, materials));
    }

    [HttpPost]
    public async Task<ActionResult<ProductionLineDto>> Create(UpsertProductionLineRequest request)
    {
        if (request == null) return BadRequest();

        var lineId = (request.LineId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(lineId)) return BadRequest("LineId is required.");

        if (await db.ProductionLineDefinitions.AnyAsync(l => l.LineId == lineId))
        {
            return Conflict($"Line '{lineId}' already exists.");
        }

        var output = await EnsureMaterialAsync(request.OutputMaterial);

        var line = new ProductionLineDefinition
        {
            LineId = lineId,
            LineName = request.LineName ?? lineId,
            Description = request.Description,
            IsActive = request.IsActive,
            OutputMaterialNumber = output.MaterialNumber
        };

        foreach (var inputReq in request.Inputs ?? new List<LineInputRequest>())
        {
            var inputMaterial = await EnsureMaterialAsync(inputReq.Material);
            var qtyPerUnit = inputReq.QuantityPerUnit;
            if (qtyPerUnit <= 0) qtyPerUnit = 1;

            line.Inputs.Add(new ProductionLineInput
            {
                MaterialNumber = inputMaterial.MaterialNumber,
                QuantityPerUnit = qtyPerUnit,
                UnitOfMeasure = string.IsNullOrWhiteSpace(inputReq.UnitOfMeasure)
                    ? inputMaterial.BaseUnitOfMeasure
                    : inputReq.UnitOfMeasure.Trim()
            });
        }

        db.ProductionLineDefinitions.Add(line);
        await db.SaveChangesAsync();

        return await GetOne(line.LineId);
    }

    [HttpPut("{lineId}")]
    public async Task<ActionResult<ProductionLineDto>> Update(string lineId, UpsertProductionLineRequest request)
    {
        var id = (lineId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(id)) return BadRequest("LineId is required.");
        if (request == null) return BadRequest();

        var line = await db.ProductionLineDefinitions
            .Include(l => l.Inputs)
            .FirstOrDefaultAsync(l => l.LineId == id);

        if (line == null) return NotFound();

        var output = await EnsureMaterialAsync(request.OutputMaterial);

        line.LineName = request.LineName ?? line.LineName;
        line.Description = request.Description;
        line.IsActive = request.IsActive;
        line.OutputMaterialNumber = output.MaterialNumber;

        // Replace inputs
        line.Inputs.Clear();
        foreach (var inputReq in request.Inputs ?? new List<LineInputRequest>())
        {
            var inputMaterial = await EnsureMaterialAsync(inputReq.Material);
            var qtyPerUnit = inputReq.QuantityPerUnit;
            if (qtyPerUnit <= 0) qtyPerUnit = 1;

            line.Inputs.Add(new ProductionLineInput
            {
                MaterialNumber = inputMaterial.MaterialNumber,
                QuantityPerUnit = qtyPerUnit,
                UnitOfMeasure = string.IsNullOrWhiteSpace(inputReq.UnitOfMeasure)
                    ? inputMaterial.BaseUnitOfMeasure
                    : inputReq.UnitOfMeasure.Trim()
            });
        }

        await db.SaveChangesAsync();

        return await GetOne(line.LineId);
    }

    [HttpDelete("{lineId}")]
    public async Task<IActionResult> Delete(string lineId)
    {
        var id = (lineId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(id)) return BadRequest("LineId is required.");

        var line = await db.ProductionLineDefinitions
            .Include(l => l.Inputs)
            .FirstOrDefaultAsync(l => l.LineId == id);

        if (line == null) return NotFound();

        db.ProductionLineDefinitions.Remove(line);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{lineId}/duplicate")]
    public async Task<ActionResult<ProductionLineDto>> Duplicate(string lineId, DuplicateProductionLineRequest request)
    {
        var id = (lineId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(id)) return BadRequest("LineId is required.");
        if (request == null) return BadRequest();

        var newLineId = (request.NewLineId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(newLineId)) return BadRequest("NewLineId is required.");

        if (await db.ProductionLineDefinitions.AnyAsync(l => l.LineId == newLineId))
        {
            return Conflict($"Line '{newLineId}' already exists.");
        }

        var src = await db.ProductionLineDefinitions.AsNoTracking()
            .Include(l => l.Inputs)
            .FirstOrDefaultAsync(l => l.LineId == id);

        if (src == null) return NotFound();

        var clone = new ProductionLineDefinition
        {
            LineId = newLineId,
            LineName = string.IsNullOrWhiteSpace(request.NewLineName) ? src.LineName : request.NewLineName.Trim(),
            Description = src.Description,
            IsActive = src.IsActive,
            OutputMaterialNumber = src.OutputMaterialNumber,
            Inputs = src.Inputs.Select(i => new ProductionLineInput
            {
                MaterialNumber = i.MaterialNumber,
                QuantityPerUnit = i.QuantityPerUnit,
                UnitOfMeasure = i.UnitOfMeasure
            }).ToList()
        };

        db.ProductionLineDefinitions.Add(clone);
        await db.SaveChangesAsync();

        return await GetOne(clone.LineId);
    }

    [HttpPost("{lineId}/produce")]
    public async Task<IActionResult> PostProduction(string lineId, PostProductionRequest request)
    {
        var id = (lineId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(id)) return BadRequest("LineId is required.");
        if (request == null) return BadRequest();

        var orderNumber = (request.OrderNumber ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(orderNumber)) return BadRequest("OrderNumber is required.");
        if (request.Quantity <= 0) return BadRequest("Quantity must be >= 1.");

        var line = await db.ProductionLineDefinitions
            .Include(l => l.Inputs)
            .FirstOrDefaultAsync(l => l.LineId == id);

        if (line == null) return NotFound();

        // Ensure output material exists
        var outputMaterial = await db.Materials.FirstOrDefaultAsync(m => m.MaterialNumber == line.OutputMaterialNumber);
        if (outputMaterial == null)
        {
            return Problem($"Output material '{line.OutputMaterialNumber}' not found for line '{id}'.");
        }

        // Consume inputs
        foreach (var input in line.Inputs)
        {
            var inputMaterial = await db.Materials.FirstOrDefaultAsync(m => m.MaterialNumber == input.MaterialNumber);
            if (inputMaterial == null)
            {
                return Problem($"Input material '{input.MaterialNumber}' not found for line '{id}'.");
            }

            var required = input.QuantityPerUnit * request.Quantity;
            inputMaterial.StockQuantity -= required;

            db.StockMovements.Add(new StockMovement
            {
                DocumentNumber = $"GI-{orderNumber}-{DateTime.UtcNow:HHmmss}",
                MovementType = "261",
                MaterialNumber = inputMaterial.MaterialNumber,
                Quantity = required,
                UnitOfMeasure = string.IsNullOrWhiteSpace(input.UnitOfMeasure) ? inputMaterial.BaseUnitOfMeasure : input.UnitOfMeasure,
                ProductionOrderNumber = orderNumber,
                PostingDate = DateTime.UtcNow,
                DocumentDate = DateTime.UtcNow,
                Status = MovementStatus.Completed,
                RobotExecuted = false
            });
        }

        // Produce output
        outputMaterial.StockQuantity += request.Quantity;
        db.StockMovements.Add(new StockMovement
        {
            DocumentNumber = $"GR-{orderNumber}-{DateTime.UtcNow:HHmmss}",
            MovementType = "101",
            MaterialNumber = outputMaterial.MaterialNumber,
            Quantity = request.Quantity,
            UnitOfMeasure = outputMaterial.BaseUnitOfMeasure,
            ProductionOrderNumber = orderNumber,
            PostingDate = DateTime.UtcNow,
            DocumentDate = DateTime.UtcNow,
            Status = MovementStatus.Completed,
            RobotExecuted = false
        });

        // Production order record
        var poExists = await db.ProductionOrders.AnyAsync(po => po.OrderNumber == orderNumber);
        if (!poExists)
        {
            db.ProductionOrders.Add(new ProductionOrder
            {
                OrderNumber = orderNumber,
                MaterialNumber = outputMaterial.MaterialNumber,
                PlannedQuantity = request.Quantity,
                ActualQuantity = request.Quantity,
                ScrapQuantity = 0,
                WorkCenter = line.LineId,
                PlannedStartDate = DateTime.UtcNow,
                PlannedEndDate = DateTime.UtcNow,
                ActualStartDate = DateTime.UtcNow,
                ActualEndDate = DateTime.UtcNow,
                Status = ProductionOrderStatus.Completed,
                Priority = 0
            });
        }

        await db.SaveChangesAsync();

        return Ok(new { lineId = line.LineId, orderNumber, quantity = request.Quantity });
    }

    private async Task<Material> EnsureMaterialAsync(MaterialRefRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var materialNumber = (request.MaterialNumber ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(materialNumber))
        {
            throw new InvalidOperationException("MaterialNumber is required.");
        }

        var existing = await db.Materials.FirstOrDefaultAsync(m => m.MaterialNumber == materialNumber);
        if (existing != null)
        {
            // Optional master-data refresh
            if (!string.IsNullOrWhiteSpace(request.Description)) existing.Description = request.Description.Trim();
            if (!string.IsNullOrWhiteSpace(request.UnitOfMeasure)) existing.BaseUnitOfMeasure = request.UnitOfMeasure.Trim();
            if (request.MaterialType != null) existing.MaterialType = request.MaterialType.Value;
            if (request.InitialStockQuantity != null) existing.StockQuantity = request.InitialStockQuantity.Value;
            if (request.MaterialType != null) existing.Status = RecordStatus.Active;

            await db.SaveChangesAsync();
            return existing;
        }

        var created = new Material
        {
            MaterialNumber = materialNumber,
            Description = string.IsNullOrWhiteSpace(request.Description) ? materialNumber : request.Description.Trim(),
            MaterialType = request.MaterialType ?? MaterialType.RawMaterial,
            BaseUnitOfMeasure = string.IsNullOrWhiteSpace(request.UnitOfMeasure) ? "PC" : request.UnitOfMeasure.Trim(),
            StockQuantity = request.InitialStockQuantity ?? 0,
            MinimumStock = 0,
            MaximumStock = 0,
            Status = RecordStatus.Active
        };

        db.Materials.Add(created);
        await db.SaveChangesAsync();
        return created;
    }

    private static ProductionLineDto MapLine(ProductionLineDefinition line, Dictionary<string, Material> materials)
    {
        var outputMaterialNumber = (line.OutputMaterialNumber ?? string.Empty).Trim();
        Material? output = null;
        if (!string.IsNullOrWhiteSpace(outputMaterialNumber))
        {
            materials.TryGetValue(outputMaterialNumber, out output);
        }

        var inputs = line.Inputs
            .OrderBy(i => i.MaterialNumber)
            .Select(i =>
            {
                var inputMaterialNumber = (i.MaterialNumber ?? string.Empty).Trim();
                Material? mat = null;
                if (!string.IsNullOrWhiteSpace(inputMaterialNumber))
                {
                    materials.TryGetValue(inputMaterialNumber, out mat);
                }
                return new ProductionLineInputDto(
                    inputMaterialNumber,
                    mat?.Description ?? inputMaterialNumber,
                    i.QuantityPerUnit,
                    string.IsNullOrWhiteSpace(i.UnitOfMeasure) ? (mat?.BaseUnitOfMeasure ?? "") : i.UnitOfMeasure);
            })
            .ToList();

        return new ProductionLineDto(
            line.LineId,
            line.LineName,
            line.Description,
            line.IsActive,
            new MaterialSummaryDto(
                outputMaterialNumber,
                output?.Description ?? outputMaterialNumber,
                output?.MaterialType ?? MaterialType.FinishedProduct,
                output?.BaseUnitOfMeasure ?? "",
                output?.StockQuantity ?? 0),
            inputs);
    }
}

public record MaterialSummaryDto(
    string MaterialNumber,
    string Description,
    MaterialType MaterialType,
    string UnitOfMeasure,
    decimal StockQuantity);

public record ProductionLineInputDto(
    string MaterialNumber,
    string Description,
    decimal QuantityPerUnit,
    string UnitOfMeasure);

public record ProductionLineDto(
    string LineId,
    string LineName,
    string? Description,
    bool IsActive,
    MaterialSummaryDto Output,
    List<ProductionLineInputDto> Inputs);

public record UpsertProductionLineRequest
{
    public string? LineId { get; init; }
    public string? LineName { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; }

    public MaterialRefRequest OutputMaterial { get; init; } = new();

    public List<LineInputRequest>? Inputs { get; init; }
}

public record LineInputRequest
{
    public MaterialRefRequest Material { get; init; } = new();

    public decimal QuantityPerUnit { get; init; }

    public string? UnitOfMeasure { get; init; }
}

public record MaterialRefRequest
{
    public string? MaterialNumber { get; init; }

    public string? Description { get; init; }

    public MaterialType? MaterialType { get; init; }

    public string? UnitOfMeasure { get; init; }

    public decimal? InitialStockQuantity { get; init; }
}

public record DuplicateProductionLineRequest
{
    public string? NewLineId { get; init; }
    public string? NewLineName { get; init; }
}

public record PostProductionRequest
{
    public string? OrderNumber { get; init; }
    public decimal Quantity { get; init; }
}
