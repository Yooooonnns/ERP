 using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DigitalisationERP.Desktop.Services;

public sealed class LeaveRequestService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _storagePath;

    public LeaveRequestService()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DigitalisationERP");
        Directory.CreateDirectory(baseDir);

        _storagePath = Path.Combine(baseDir, "leave-requests.json");
    }

    public IReadOnlyList<LeaveRequestEntry> GetAll()
    {
        var data = Load();
        return data.Requests
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
    }

    public IReadOnlyList<LeaveRequestEntry> GetForUser(string userId)
    {
        return GetAll()
            .Where(r => string.Equals(r.UserId, userId, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public void Submit(LeaveRequestEntry request)
    {
        var data = Load();
        data.Requests.Add(request with
        {
            Id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString("N") : request.Id,
            CreatedAt = request.CreatedAt == default ? DateTimeOffset.Now : request.CreatedAt,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Submitted" : request.Status
        });
        Save(data);
    }

    private LeaveRequestData Load()
    {
        try
        {
            if (!File.Exists(_storagePath))
            {
                return new LeaveRequestData();
            }

            var json = File.ReadAllText(_storagePath);
            return JsonSerializer.Deserialize<LeaveRequestData>(json, JsonOptions) ?? new LeaveRequestData();
        }
        catch
        {
            return new LeaveRequestData();
        }
    }

    private void Save(LeaveRequestData data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(_storagePath, json);
    }
}

public sealed record LeaveRequestEntry
{
    public string Id { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public string Status { get; init; } = string.Empty;
}

internal sealed class LeaveRequestData
{
    public List<LeaveRequestEntry> Requests { get; set; } = new();
}
