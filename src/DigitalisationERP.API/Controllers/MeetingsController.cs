using System.Security.Claims;
using DigitalisationERP.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalisationERP.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MeetingsController : ControllerBase
{
    private readonly JsonFileStore<List<MeetingItem>> _store;

    public MeetingsController()
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, "App_Data", "meetings.json");
        _store = new JsonFileStore<List<MeetingItem>>(filePath);
    }

    [HttpGet("mine")]
    public async Task<ActionResult<List<MeetingItem>>> GetMine(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var meetings = await _store.ReadAsync(static () => new List<MeetingItem>(), cancellationToken);

        var mine = meetings
            .Where(m => m.AttendeeIds.Contains(userId) || m.CreatedByUserId == userId)
            .OrderBy(m => m.StartUtc)
            .ToList();

        return Ok(mine);
    }

    [HttpPost]
    public async Task<ActionResult<MeetingItem>> Create([FromBody] CreateMeetingRequest request, CancellationToken cancellationToken)
    {
        if (!CanCreateMeeting())
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { message = "Title is required" });

        if (request.EndUtc <= request.StartUtc)
            return BadRequest(new { message = "EndUtc must be after StartUtc" });

        var userId = GetUserId();
        var userName = User.FindFirstValue(ClaimTypes.Name) ?? "Unknown";

        var meetings = await _store.ReadAsync(static () => new List<MeetingItem>(), cancellationToken);

        var item = new MeetingItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = request.Title.Trim(),
            Description = (request.Description ?? string.Empty).Trim(),
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc,
            CreatedByUserId = userId,
            CreatedByName = userName,
            CreatedAtUtc = DateTime.UtcNow,
            AttendeeIds = request.AttendeeIds?.Distinct().ToList() ?? new List<int>()
        };

        // Ensure creator is included.
        if (!item.AttendeeIds.Contains(userId))
            item.AttendeeIds.Add(userId);

        meetings.Add(item);
        await _store.WriteAsync(meetings, cancellationToken);

        return Ok(item);
    }

    [HttpPatch("{id}/ack")]
    public async Task<IActionResult> Acknowledge(string id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var meetings = await _store.ReadAsync(static () => new List<MeetingItem>(), cancellationToken);
        var meeting = meetings.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
        if (meeting == null)
            return NotFound();

        if (!meeting.AttendeeIds.Contains(userId) && meeting.CreatedByUserId != userId)
            return Forbid();

        if (!meeting.AcknowledgedBy.Contains(userId))
        {
            meeting.AcknowledgedBy.Add(userId);
            await _store.WriteAsync(meetings, cancellationToken);
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var roles = GetRoles();

        var meetings = await _store.ReadAsync(static () => new List<MeetingItem>(), cancellationToken);
        var meeting = meetings.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
        if (meeting == null)
            return NotFound();

        var isOwner = meeting.CreatedByUserId == userId;
        var isAdmin = roles.Contains("S_USER", StringComparer.OrdinalIgnoreCase);

        if (!isOwner && !isAdmin)
            return Forbid();

        meetings.Remove(meeting);
        await _store.WriteAsync(meetings, cancellationToken);
        return NoContent();
    }

    private int GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : 0;
    }

    private List<string> GetRoles()
        => User.FindAll(ClaimTypes.Role).Select(r => r.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

    private bool CanCreateMeeting()
    {
        var roles = GetRoles();
        // Allow managers/leaders/planners + S_USER.
        return roles.Any(r =>
            r.Equals("S_USER", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("MANAGER", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("LEADER", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("PLANNER", StringComparison.OrdinalIgnoreCase));
    }

    public sealed class CreateMeetingRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public List<int>? AttendeeIds { get; set; }
    }

    public sealed class MeetingItem
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public int CreatedByUserId { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public List<int> AttendeeIds { get; set; } = new();
        public List<int> AcknowledgedBy { get; set; } = new();
    }
}
