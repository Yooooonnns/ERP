using DigitalisationERP.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalisationERP.API.Controllers;

[Authorize]
[ApiController]
[Route("api/shifts")]
public sealed class ShiftsController : ControllerBase
{
    private readonly JsonFileStore<ShiftScheduleData> _store;

    public ShiftsController(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "App_Data", "shift-schedule.json");
        _store = new JsonFileStore<ShiftScheduleData>(path);
    }

    [HttpGet("week")]
    public async Task<ActionResult<List<ShiftEntryDto>>> GetWeek([FromQuery] string weekStart, [FromQuery] string? employeeId = null, CancellationToken cancellationToken = default)
    {
        if (!DateTime.TryParse(weekStart, out var start))
        {
            return BadRequest(new { message = "Invalid weekStart. Use ISO date like 2026-01-11." });
        }

        start = start.Date;
        var endExclusive = start.AddDays(7);

        var data = await _store.ReadAsync(() => new ShiftScheduleData(), cancellationToken);

        var shifts = data.Shifts
            .Where(s => s.Date >= start && s.Date < endExclusive);

        if (!string.IsNullOrWhiteSpace(employeeId))
        {
            shifts = shifts.Where(s => string.Equals(s.EmployeeId, employeeId, StringComparison.OrdinalIgnoreCase));
        }

        var list = shifts
            .OrderBy(s => s.Date)
            .ThenBy(s => s.Segment)
            .ToList();

        if (list.Count == 0 && !string.IsNullOrWhiteSpace(employeeId))
        {
            var seeded = SeedDefaultWeek(employeeId, start);
            foreach (var shift in seeded)
            {
                data.Upsert(shift);
            }

            await _store.WriteAsync(data, cancellationToken);

            list = data.Shifts
                .Where(s => s.Date >= start && s.Date < endExclusive)
                .Where(s => string.Equals(s.EmployeeId, employeeId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Date)
                .ThenBy(s => s.Segment)
                .ToList();
        }

        return Ok(list);
    }

    [HttpPost("upsert")]
    public async Task<ActionResult> Upsert([FromBody] List<ShiftEntryDto> shifts, CancellationToken cancellationToken = default)
    {
        if (shifts == null) return BadRequest();

        var data = await _store.ReadAsync(() => new ShiftScheduleData(), cancellationToken);

        foreach (var shift in shifts)
        {
            var normalized = shift with
            {
                Id = string.IsNullOrWhiteSpace(shift.Id) ? Guid.NewGuid().ToString("N") : shift.Id,
                Date = shift.Date.Date
            };

            data.Upsert(normalized);
        }

        await _store.WriteAsync(data, cancellationToken);
        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<ShiftEntryDto>> Add([FromBody] ShiftEntryDto shift, CancellationToken cancellationToken = default)
    {
        if (shift == null) return BadRequest();

        var data = await _store.ReadAsync(() => new ShiftScheduleData(), cancellationToken);

        var created = shift with
        {
            Id = string.IsNullOrWhiteSpace(shift.Id) ? Guid.NewGuid().ToString("N") : shift.Id,
            Date = shift.Date.Date
        };

        data.Upsert(created);
        await _store.WriteAsync(data, cancellationToken);

        return Ok(created);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return BadRequest();

        var data = await _store.ReadAsync(() => new ShiftScheduleData(), cancellationToken);
        data.Shifts.RemoveAll(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
        await _store.WriteAsync(data, cancellationToken);

        return NoContent();
    }

    private static IReadOnlyList<ShiftEntryDto> SeedDefaultWeek(string employeeId, DateTime weekStart)
    {
        return new List<ShiftEntryDto>
        {
            new() { EmployeeId = employeeId, Date = weekStart.AddDays(0), Segment = 0, StartTime = "07:00", EndTime = "15:00", Location = "POST-02" },
            new() { EmployeeId = employeeId, Date = weekStart.AddDays(1), Segment = 0, StartTime = "07:00", EndTime = "15:00", Location = "POST-02" },
            new() { EmployeeId = employeeId, Date = weekStart.AddDays(2), Segment = 0, StartTime = "07:00", EndTime = "15:00", Location = "POST-02" },
            new() { EmployeeId = employeeId, Date = weekStart.AddDays(3), Segment = 1, StartTime = "15:00", EndTime = "23:00", Location = "POST-03" },
            new() { EmployeeId = employeeId, Date = weekStart.AddDays(4), Segment = 1, StartTime = "15:00", EndTime = "23:00", Location = "POST-03" }
        };
    }
}

public sealed record ShiftEntryDto
{
    public string Id { get; init; } = string.Empty;
    public string EmployeeId { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public int Segment { get; init; }
    public string StartTime { get; init; } = string.Empty;
    public string EndTime { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
}

internal sealed class ShiftScheduleData
{
    public List<ShiftEntryDto> Shifts { get; set; } = new();

    public void Upsert(ShiftEntryDto shift)
    {
        var id = shift.Id;
        var index = Shifts.FindIndex(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            Shifts[index] = shift;
        }
        else
        {
            Shifts.Add(shift);
        }
    }
}
