using DigitalisationERP.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalisationERP.API.Controllers;

[Authorize]
[ApiController]
[Route("api/leave-requests")]
public sealed class LeaveRequestsController : ControllerBase
{
    private readonly JsonFileStore<LeaveRequestData> _store;

    public LeaveRequestsController(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "App_Data", "leave-requests.json");
        _store = new JsonFileStore<LeaveRequestData>(path);
    }

    [HttpGet]
    public async Task<ActionResult<List<LeaveRequestEntryDto>>> GetAll([FromQuery] string? userId = null, CancellationToken cancellationToken = default)
    {
        var data = await _store.ReadAsync(() => new LeaveRequestData(), cancellationToken);
        var list = data.Requests.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            list = list.Where(r => string.Equals(r.UserId, userId, StringComparison.OrdinalIgnoreCase));
        }

        return Ok(list.OrderByDescending(r => r.CreatedAt).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<LeaveRequestEntryDto>> Submit([FromBody] LeaveRequestEntryDto request, CancellationToken cancellationToken = default)
    {
        if (request == null) return BadRequest();

        var data = await _store.ReadAsync(() => new LeaveRequestData(), cancellationToken);
        var created = request with
        {
            Id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString("N") : request.Id,
            CreatedAt = request.CreatedAt == default ? DateTimeOffset.UtcNow : request.CreatedAt,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Submitted" : request.Status,
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate.Date
        };

        data.Requests.Add(created);
        await _store.WriteAsync(data, cancellationToken);

        return Ok(created);
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult> UpdateStatus(string id, [FromBody] LeaveRequestUpdateStatusRequest body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return BadRequest();
        if (body == null || string.IsNullOrWhiteSpace(body.Status)) return BadRequest();

        var data = await _store.ReadAsync(() => new LeaveRequestData(), cancellationToken);
        var index = data.Requests.FindIndex(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return NotFound();

        data.Requests[index] = data.Requests[index] with { Status = body.Status };
        await _store.WriteAsync(data, cancellationToken);

        return NoContent();
    }
}

public sealed record LeaveRequestEntryDto
{
    public string Id { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public string Status { get; init; } = string.Empty;
}

public sealed record LeaveRequestUpdateStatusRequest
{
    public string Status { get; init; } = string.Empty;
}

internal sealed class LeaveRequestData
{
    public List<LeaveRequestEntryDto> Requests { get; set; } = new();
}
