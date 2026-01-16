using DigitalisationERP.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalisationERP.API.Controllers;

[Authorize]
[ApiController]
[Route("api/task-feedback")]
public sealed class TaskFeedbackController : ControllerBase
{
    private readonly JsonFileStore<TaskFeedbackData> _store;

    public TaskFeedbackController(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "App_Data", "task-feedback.json");
        _store = new JsonFileStore<TaskFeedbackData>(path);
    }

    [HttpGet]
    public async Task<ActionResult<List<TaskFeedbackEntryDto>>> Get([FromQuery] string? status = null, CancellationToken cancellationToken = default)
    {
        var data = await _store.ReadAsync(() => new TaskFeedbackData(), cancellationToken);
        var list = data.Items.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            list = list.Where(i => string.Equals(i.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        return Ok(list.OrderByDescending(i => i.CreatedAt).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<TaskFeedbackEntryDto>> Submit([FromBody] TaskFeedbackEntryDto entry, CancellationToken cancellationToken = default)
    {
        if (entry == null) return BadRequest();

        var data = await _store.ReadAsync(() => new TaskFeedbackData(), cancellationToken);
        var created = entry with
        {
            Id = string.IsNullOrWhiteSpace(entry.Id) ? Guid.NewGuid().ToString("N") : entry.Id,
            CreatedAt = entry.CreatedAt == default ? DateTimeOffset.UtcNow : entry.CreatedAt,
            Status = string.IsNullOrWhiteSpace(entry.Status) ? "New" : entry.Status
        };

        data.Items.Add(created);
        await _store.WriteAsync(data, cancellationToken);

        return Ok(created);
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult> UpdateStatus(string id, [FromBody] TaskFeedbackUpdateStatusRequest body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return BadRequest();
        if (body == null || string.IsNullOrWhiteSpace(body.Status)) return BadRequest();

        var data = await _store.ReadAsync(() => new TaskFeedbackData(), cancellationToken);
        var index = data.Items.FindIndex(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return NotFound();

        data.Items[index] = data.Items[index] with { Status = body.Status };
        await _store.WriteAsync(data, cancellationToken);

        return NoContent();
    }
}

public sealed record TaskFeedbackEntryDto
{
    public string Id { get; init; } = string.Empty;
    public string TaskNumber { get; init; } = string.Empty;
    public string TaskTitle { get; init; } = string.Empty;
    public string SubmittedBy { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public string Status { get; init; } = string.Empty;
}

public sealed record TaskFeedbackUpdateStatusRequest
{
    public string Status { get; init; } = string.Empty;
}

internal sealed class TaskFeedbackData
{
    public List<TaskFeedbackEntryDto> Items { get; set; } = new();
}
