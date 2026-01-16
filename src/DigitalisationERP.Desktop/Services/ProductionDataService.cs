using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DigitalisationERP.Desktop.Controls;

namespace DigitalisationERP.Desktop.Services;

public class ProductionPostData
{
    public string PostCode { get; set; } = "";
    public string PostName { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public int Position { get; set; } // Position in the line sequence
    public string LineId { get; set; } = ""; // Which production line this post belongs to
    public int CurrentLoad { get; set; }
    public int MaterialLevel { get; set; }
    public int UtilityTimeSeconds { get; set; }
    public int StockCapacity { get; set; }
    public string Status { get; set; } = "";
    public double MaintenanceHealthScore { get; set; }
    public string MaintenanceIssue { get; set; } = "";
    public bool IsSelected { get; set; } = false; // For multi-select functionality
}

public class ProductionLineData
{
    public string LineId { get; set; } = "";
    public string LineName { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public ObservableCollection<ProductionPostData> Posts { get; set; } = new();

    public int FinishedProductCount { get; set; }
    
    // Calculated properties
    public double AverageHealthScore => Posts.Any() ? Posts.Average(p => p.MaintenanceHealthScore) : 0;
    public int TotalPosts => Posts.Count;
    public int CriticalPosts => Posts.Count(p => p.MaintenanceHealthScore < 50);
    public int WarningPosts => Posts.Count(p => p.MaintenanceHealthScore >= 50 && p.MaintenanceHealthScore < 70);
    public double TotalCapacity => Posts.Sum(p => p.StockCapacity);
    public double CurrentUtilization => TotalCapacity > 0 ? (Posts.Sum(p => p.CurrentLoad) / TotalCapacity * 100) : 0;
}

public class ProductionDataService
{
    private static ProductionDataService? _instance;
    private readonly ObservableCollection<ProductionLineData> _productionLines = new();
    private readonly ObservableCollection<ProductionPostData> _posts = new(); // Legacy - for backward compatibility
    private bool _isInitialized = false;

    public static ProductionDataService Instance => _instance ??= new ProductionDataService();

    public event EventHandler<ProductionPostData>? PostAdded;
    public event EventHandler<ProductionPostData>? PostUpdated;
    public event EventHandler<string>? PostRemoved;
    public event EventHandler<ProductionLineData>? LineAdded;
    public event EventHandler<string>? LineRemoved;
    public event EventHandler<ProductionLineData>? LineUpdated;

    private ProductionDataService()
    {
        // Don't auto-load data - let pages initialize explicitly
    }

    public void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            _isInitialized = true;
            LoadFromDisk();
        }
    }

    public ObservableCollection<ProductionLineData> ProductionLines => _productionLines;
    public ObservableCollection<ProductionPostData> Posts => _posts; // Legacy support

    // Production Line Management
    public void AddProductionLine(ProductionLineData line)
    {
        _productionLines.Add(line);
        LineAdded?.Invoke(this, line);
        SaveToDisk();
    }

    public void RemoveProductionLine(string lineId)
    {
        var line = _productionLines.FirstOrDefault(l => l.LineId == lineId);
        if (line != null)
        {
            _productionLines.Remove(line);
            LineRemoved?.Invoke(this, lineId);
            SaveToDisk();
        }
    }

    public ProductionLineData? GetProductionLine(string lineId)
    {
        return _productionLines.FirstOrDefault(l => l.LineId == lineId);
    }

    // Post Management
    public void AddPost(ProductionPostData post)
    {
        _posts.Add(post);
        
        // Also add to the specific line
        var line = _productionLines.FirstOrDefault(l => l.LineId == post.LineId);
        if (line != null)
        {
            post.Position = line.Posts.Count; // Auto-assign position
            line.Posts.Add(post);
        }
        
        PostAdded?.Invoke(this, post);
        SaveToDisk();
    }

    public void AddPostToLine(string lineId, ProductionPostData post, int? position = null)
    {
        var line = _productionLines.FirstOrDefault(l => l.LineId == lineId);
        if (line == null) return;

        post.LineId = lineId;
        
        if (position.HasValue)
        {
            // Insert at specific position and reorder
            post.Position = position.Value;
            line.Posts.Insert(position.Value, post);
            ReorderLinePosts(lineId);
        }
        else
        {
            // Add at end
            post.Position = line.Posts.Count;
            line.Posts.Add(post);
        }
        
        _posts.Add(post);
        PostAdded?.Invoke(this, post);

        SaveToDisk();
    }

    public void UpdatePost(string postCode, Action<ProductionPostData> updateAction)
    {
        var post = _posts.FirstOrDefault(p => p.PostCode == postCode);
        if (post != null)
        {
            updateAction(post);
            PostUpdated?.Invoke(this, post);
            SaveToDisk();
        }
    }

    public void UpdateProductionLine(string lineId, Action<ProductionLineData> updateAction)
    {
        var line = _productionLines.FirstOrDefault(l => l.LineId == lineId);
        if (line == null) return;

        updateAction(line);
        LineUpdated?.Invoke(this, line);
        SaveToDisk();
    }

    public void RemovePost(string postCode)
    {
        var post = _posts.FirstOrDefault(p => p.PostCode == postCode);
        if (post != null)
        {
            _posts.Remove(post);
            
            // Remove from line
            var line = _productionLines.FirstOrDefault(l => l.LineId == post.LineId);
            if (line != null)
            {
                line.Posts.Remove(post);
                ReorderLinePosts(line.LineId);
            }
            
            PostRemoved?.Invoke(this, postCode);

            SaveToDisk();
        }
    }

    public void SwapPosts(string postCode1, string postCode2)
    {
        var post1 = _posts.FirstOrDefault(p => p.PostCode == postCode1);
        var post2 = _posts.FirstOrDefault(p => p.PostCode == postCode2);
        
        if (post1 == null || post2 == null || post1.LineId != post2.LineId) return;

        // Swap positions
        (post1.Position, post2.Position) = (post2.Position, post1.Position);
        
        var line = _productionLines.FirstOrDefault(l => l.LineId == post1.LineId);
        if (line != null)
        {
            ReorderLinePosts(line.LineId);
        }
        
        PostUpdated?.Invoke(this, post1);
        PostUpdated?.Invoke(this, post2);

        SaveToDisk();
    }

    private void ReorderLinePosts(string lineId)
    {
        var line = _productionLines.FirstOrDefault(l => l.LineId == lineId);
        if (line == null) return;

        var orderedPosts = line.Posts.OrderBy(p => p.Position).ToList();
        line.Posts.Clear();
        
        for (int i = 0; i < orderedPosts.Count; i++)
        {
            orderedPosts[i].Position = i;
            line.Posts.Add(orderedPosts[i]);
        }
    }

    public ProductionPostData? GetPost(string postCode)
    {
        return _posts.FirstOrDefault(p => p.PostCode == postCode);
    }

    public IEnumerable<ProductionPostData> GetPostsForLine(string lineId)
    {
        return _posts.Where(p => p.LineId == lineId).OrderBy(p => p.Position);
    }

    public IEnumerable<ProductionPostData> GetPostsNeedingMaintenance()
    {
        return _posts.Where(p => p.MaintenanceHealthScore < 70).OrderBy(p => p.MaintenanceHealthScore);
    }

    public IEnumerable<ProductionPostData> GetSelectedPosts()
    {
        return _posts.Where(p => p.IsSelected);
    }

    public void ClearSelection()
    {
        foreach (var post in _posts.Where(p => p.IsSelected))
        {
            post.IsSelected = false;
        }
    }

    private sealed class ProductionDataStore
    {
        [JsonPropertyName("lines")]
        public List<ProductionLineData> Lines { get; set; } = new();
    }

    private static string GetDataFilePath()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DigitalisationERP");
        Directory.CreateDirectory(baseDir);
        return Path.Combine(baseDir, "production_data.json");
    }

    private void LoadFromDisk()
    {
        try
        {
            var path = GetDataFilePath();
            if (!File.Exists(path))
            {
                return;
            }

            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return;

            var store = JsonSerializer.Deserialize<ProductionDataStore>(json);
            if (store == null) return;

            _productionLines.Clear();
            _posts.Clear();

            foreach (var line in store.Lines)
            {
                // Ensure Posts collection is non-null
                line.Posts ??= new ObservableCollection<ProductionPostData>();

                _productionLines.Add(line);
                foreach (var post in line.Posts)
                {
                    if (!string.Equals(post.LineId, line.LineId, StringComparison.OrdinalIgnoreCase))
                    {
                        post.LineId = line.LineId;
                    }
                    _posts.Add(post);
                }
            }
        }
        catch
        {
            // If file is corrupted, start empty rather than crashing the app.
        }
    }

    private void SaveToDisk()
    {
        try
        {
            var path = GetDataFilePath();
            var store = new ProductionDataStore
            {
                Lines = _productionLines.ToList()
            };

            var json = JsonSerializer.Serialize(store, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(path, json);
        }
        catch
        {
            // Ignore persistence failures (disk permissions, etc.)
        }
    }

    public void ClearAll()
    {
        _posts.Clear();
        _productionLines.Clear();
        SaveToDisk();
    }
}
