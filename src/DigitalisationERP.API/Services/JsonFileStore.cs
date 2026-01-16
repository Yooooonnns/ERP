using System.Text.Json;

namespace DigitalisationERP.API.Services;

/// <summary>
/// Minimal JSON file store for simple API-backed features.
/// Keeps persistence on the server side without requiring DB migrations.
/// </summary>
public sealed class JsonFileStore<TData> where TData : class
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonFileStore(string filePath)
    {
        _filePath = filePath;
    }

    public async Task<TData> ReadAsync(Func<TData> defaultFactory, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_filePath))
            {
                return defaultFactory();
            }

            var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
            return JsonSerializer.Deserialize<TData>(json, JsonOptions) ?? defaultFactory();
        }
        catch
        {
            return defaultFactory();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task WriteAsync(TData data, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(data, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }
}
