using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DigitalisationERP.Desktop.Services;

public sealed class ShiftScheduleService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _storagePath;

    public ShiftScheduleService()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DigitalisationERP");
        Directory.CreateDirectory(baseDir);

        _storagePath = Path.Combine(baseDir, "shift-schedule.json");
    }

    public IReadOnlyList<ShiftEntry> GetAllShifts()
    {
        var data = Load();
        return data.Shifts
            .OrderBy(s => s.Date)
            .ThenBy(s => s.Segment)
            .ToList();
    }

    public IReadOnlyList<ShiftEntry> GetShiftsForWeek(DateTime weekStart, string? employeeId = null)
    {
        var start = weekStart.Date;
        var endExclusive = start.AddDays(7);

        var shifts = GetAllShifts()
            .Where(s => s.Date >= start && s.Date < endExclusive);

        if (!string.IsNullOrWhiteSpace(employeeId))
        {
            shifts = shifts.Where(s => string.Equals(s.EmployeeId, employeeId, StringComparison.OrdinalIgnoreCase));
        }

        var list = shifts.ToList();

        // If nothing exists yet for this user/week, create a simple default pattern.
        if (list.Count == 0 && !string.IsNullOrWhiteSpace(employeeId))
        {
            var seeded = SeedDefaultWeek(employeeId, start);
            UpsertShifts(seeded);
            list = GetShiftsForWeek(start, employeeId).ToList();
        }

        return list;
    }

    public void AddShift(ShiftEntry shift)
    {
        if (shift == null) throw new ArgumentNullException(nameof(shift));

        var data = Load();
        data.Shifts.Add(shift with { Id = string.IsNullOrWhiteSpace(shift.Id) ? Guid.NewGuid().ToString("N") : shift.Id });
        Save(data);
    }

    public void DeleteShift(string shiftId)
    {
        if (string.IsNullOrWhiteSpace(shiftId)) return;

        var data = Load();
        data.Shifts.RemoveAll(s => string.Equals(s.Id, shiftId, StringComparison.OrdinalIgnoreCase));
        Save(data);
    }

    public void UpsertShifts(IEnumerable<ShiftEntry> shifts)
    {
        var data = Load();
        foreach (var shift in shifts)
        {
            var id = string.IsNullOrWhiteSpace(shift.Id) ? Guid.NewGuid().ToString("N") : shift.Id;
            var existingIndex = data.Shifts.FindIndex(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

            var normalized = shift with { Id = id, Date = shift.Date.Date };

            if (existingIndex >= 0)
            {
                data.Shifts[existingIndex] = normalized;
            }
            else
            {
                data.Shifts.Add(normalized);
            }
        }

        Save(data);
    }

    private ShiftScheduleData Load()
    {
        try
        {
            if (!File.Exists(_storagePath))
            {
                return new ShiftScheduleData();
            }

            var json = File.ReadAllText(_storagePath);
            return JsonSerializer.Deserialize<ShiftScheduleData>(json, JsonOptions) ?? new ShiftScheduleData();
        }
        catch
        {
            // If parsing fails, start fresh rather than crashing the UI.
            return new ShiftScheduleData();
        }
    }

    private void Save(ShiftScheduleData data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(_storagePath, json);
    }

    private static IReadOnlyList<ShiftEntry> SeedDefaultWeek(string employeeId, DateTime weekStart)
    {
        // Simple default: Mon-Wed morning, Thu-Fri afternoon.
        return new List<ShiftEntry>
        {
            new() { EmployeeId = employeeId, Date = weekStart.AddDays(0), Segment = ShiftSegment.Morning, StartTime = "07:00", EndTime = "15:00", Location = "POST-02" },
            new() { EmployeeId = employeeId, Date = weekStart.AddDays(1), Segment = ShiftSegment.Morning, StartTime = "07:00", EndTime = "15:00", Location = "POST-02" },
            new() { EmployeeId = employeeId, Date = weekStart.AddDays(2), Segment = ShiftSegment.Morning, StartTime = "07:00", EndTime = "15:00", Location = "POST-02" },
            new() { EmployeeId = employeeId, Date = weekStart.AddDays(3), Segment = ShiftSegment.Afternoon, StartTime = "15:00", EndTime = "23:00", Location = "POST-05" },
            new() { EmployeeId = employeeId, Date = weekStart.AddDays(4), Segment = ShiftSegment.Afternoon, StartTime = "15:00", EndTime = "23:00", Location = "POST-05" }
        };
    }
}

public enum ShiftSegment
{
    Morning = 0,
    Afternoon = 1,
    Night = 2
}

public sealed record ShiftEntry
{
    public string Id { get; init; } = string.Empty;
    public string EmployeeId { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public ShiftSegment Segment { get; init; }
    public string StartTime { get; init; } = string.Empty;
    public string EndTime { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
}

internal sealed class ShiftScheduleData
{
    public List<ShiftEntry> Shifts { get; set; } = new();
}
