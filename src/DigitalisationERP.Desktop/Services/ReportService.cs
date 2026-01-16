using System.IO;
using System.Text;
using System.Text.Json;

namespace DigitalisationERP.Desktop.Services;

public sealed class ReportService
{
    public async Task ExportAsync(string filePath)
    {
        ProductionDataService.Instance.EnsureInitialized();

        var extension = Path.GetExtension(filePath);
        var content = extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
            ? BuildJsonReport()
            : BuildTextReport();

        await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
    }

    private string BuildTextReport()
    {
        var lines = ProductionDataService.Instance.ProductionLines.ToList();

        var sb = new StringBuilder();
        sb.AppendLine("DigitalisationERP - Production Report");
        sb.AppendLine($"GeneratedAt: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        if (lines.Count == 0)
        {
            sb.AppendLine("No production lines configured.");
            return sb.ToString();
        }

        foreach (var line in lines)
        {
            sb.AppendLine($"Line: {line.LineName} ({line.LineId})");
            sb.AppendLine($"Active: {line.IsActive}");
            sb.AppendLine($"FinishedProducts: {line.FinishedProductCount}");
            sb.AppendLine($"Posts: {line.Posts.Count}");
            sb.AppendLine();

            foreach (var post in line.Posts.OrderBy(p => p.Position))
            {
                sb.AppendLine(
                    $"- {post.PostCode} | Stock: {post.CurrentLoad}/{post.StockCapacity} | Health: {post.MaintenanceHealthScore:0.0} | Status: {post.Status}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string BuildJsonReport()
    {
        var lines = ProductionDataService.Instance.ProductionLines.Select(line => new
        {
            line.LineId,
            line.LineName,
            line.IsActive,
            line.CreatedDate,
            line.FinishedProductCount,
            Posts = line.Posts
                .OrderBy(p => p.Position)
                .Select(post => new
                {
                    post.PostCode,
                    post.PostName,
                    post.Position,
                    post.CurrentLoad,
                    post.StockCapacity,
                    post.MaintenanceHealthScore,
                    post.MaintenanceIssue,
                    post.Status
                })
                .ToList()
        }).ToList();

        var payload = new
        {
            GeneratedAt = DateTime.Now,
            Lines = lines
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}
