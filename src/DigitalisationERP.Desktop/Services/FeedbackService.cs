using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DigitalisationERP.Desktop.Services;

public sealed class FeedbackService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _storagePath;

    public FeedbackService()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DigitalisationERP");
        Directory.CreateDirectory(baseDir);

        _storagePath = Path.Combine(baseDir, "task-feedback.json");
    }

    public IReadOnlyList<FeedbackEntry> GetAll()
    {
        var data = Load();
        return data.Items
            .OrderByDescending(i => i.CreatedAt)
            .ToList();
    }

    public IReadOnlyList<FeedbackEntry> GetByStatus(string status)
    {
        return GetAll()
            .Where(i => string.Equals(i.Status, status, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public void Submit(FeedbackEntry entry)
    {
        var data = Load();
        data.Items.Add(entry with
        {
            Id = string.IsNullOrWhiteSpace(entry.Id) ? Guid.NewGuid().ToString("N") : entry.Id,
            CreatedAt = entry.CreatedAt == default ? DateTimeOffset.Now : entry.CreatedAt,
            Status = string.IsNullOrWhiteSpace(entry.Status) ? "New" : entry.Status
        });
        Save(data);
    }

    public void UpdateStatus(string id, string status)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        var data = Load();
        var index = data.Items.FindIndex(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return;

        data.Items[index] = data.Items[index] with { Status = status };
        Save(data);
    }

    private FeedbackData Load()
    {
        try
        {
            if (!File.Exists(_storagePath))
            {
                return new FeedbackData();
            }

            var json = File.ReadAllText(_storagePath);
            return JsonSerializer.Deserialize<FeedbackData>(json, JsonOptions) ?? new FeedbackData();
        }
        catch
        {
            return new FeedbackData();
        }
    }

    private void Save(FeedbackData data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(_storagePath, json);
    }
}

public sealed record FeedbackEntry
{
    public string Id { get; init; } = string.Empty;
    public string TaskNumber { get; init; } = string.Empty;
    public string TaskTitle { get; init; } = string.Empty;
    public string SubmittedBy { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public string Status { get; init; } = string.Empty;
}

internal sealed class FeedbackData
{
    public List<FeedbackEntry> Items { get; set; } = new();
}
