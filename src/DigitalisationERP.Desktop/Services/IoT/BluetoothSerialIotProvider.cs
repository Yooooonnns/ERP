using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DigitalisationERP.Desktop.Models;

namespace DigitalisationERP.Desktop.Services.IoT;

/// <summary>
/// Minimal IoT provider that consumes line-delimited messages from a Bluetooth serial (COM) port
/// and converts them into <see cref="IotSensorReading"/> events.
///
/// Incoming line format is intentionally flexible. Supported examples:
/// - "P1" / "POST 1" / "post=1" / "post:3 detected"
/// - JSON: {"post":1} or {"postIndex":3,"trigger":true}
///
/// The produced readings use <see cref="IotSensorReading.PostCode"/> as the post *index* string ("1", "2", "3")
/// to allow mapping to the current production route.
/// </summary>
public sealed class BluetoothSerialIotProvider : IIotProvider, IDisposable
{
    private readonly BluetoothSerialClient _client;
    private readonly bool _ownsClient;
    private readonly ConcurrentDictionary<string, IotSensorReading> _latest = new(StringComparer.OrdinalIgnoreCase);

    public string ProviderName => "BluetoothSerial";

    public bool IsConnected => _client.IsOpen;

    #pragma warning disable CS0067
    public event EventHandler<SensorReadingEventArgs>? SensorReadingReceived;
    public event EventHandler<RobotStateEventArgs>? RobotStateChanged;
    public event EventHandler<IotLogEventArgs>? LogEventAdded;
    public event EventHandler<AlertEventArgs>? CriticalAlertRaised;
    #pragma warning restore CS0067

    public BluetoothSerialIotProvider(BluetoothSerialClient client, bool ownsClient = false)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _ownsClient = ownsClient;
    }

    public async Task<bool> ConnectAsync()
    {
        try
        {
            await _client.EnsureOpenAsync().ConfigureAwait(false);
            _client.LineReceived += OnLineReceived;
            await _client.StartListeningAsync().ConfigureAwait(false);

            AddLog("BLUETOOTH", "INFO", "Bluetooth serial connected and listening.");
            return true;
        }
        catch (Exception ex)
        {
            AddLog("BLUETOOTH", "ERROR", $"Bluetooth serial connect failed: {ex.Message}");
            return false;
        }
    }

    public Task DisconnectAsync()
    {
        try
        {
            _client.LineReceived -= OnLineReceived;
        }
        catch
        {
            // ignore
        }

        if (_ownsClient)
        {
            try
            {
                _client.Dispose();
            }
            catch
            {
                // ignore
            }
        }

        AddLog("BLUETOOTH", "INFO", "Bluetooth serial disconnected.");
        return Task.CompletedTask;
    }

    public Task<IotSensorReading?> ReadSensorAsync(string sensorId)
    {
        if (string.IsNullOrWhiteSpace(sensorId)) return Task.FromResult<IotSensorReading?>(null);
        _latest.TryGetValue(sensorId, out var reading);
        return Task.FromResult(reading);
    }

    public Task<bool> SendRobotCommandAsync(string robotId, RobotCommand command, string? targetLocation = null)
    {
        // Not supported by this provider (robot output is handled by AgvRobotMessenger).
        return Task.FromResult(false);
    }

    public Task<RobotState?> GetRobotStateAsync(string robotId)
    {
        return Task.FromResult<RobotState?>(null);
    }

    public Task<IotSensorReading[]> GetAllSensorsAsync()
    {
        return Task.FromResult(_latest.Values.ToArray());
    }

    public Task<RobotState[]> GetAllRobotsAsync()
    {
        return Task.FromResult(Array.Empty<RobotState>());
    }

    public Task<bool> SetSensorThresholdAsync(string sensorId, double warningThreshold, double criticalThreshold)
    {
        // No thresholds on discrete presence triggers.
        return Task.FromResult(false);
    }

    private void OnLineReceived(object? sender, string line)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            if (!TryParsePostIndex(line, out var postIndex1Based))
            {
                AddLog("BLUETOOTH", "DEBUG", $"Unrecognized line: {line}");
                return;
            }

            // We treat any recognized post line as a "presence" trigger.
            var now = DateTime.Now;
            var sensorId = $"BT-P{postIndex1Based}";

            var reading = _latest.AddOrUpdate(
                sensorId,
                _ => new IotSensorReading
                {
                    SensorId = sensorId,
                    PostCode = postIndex1Based.ToString(),
                    Type = SensorType.Pressure,
                    Value = 1,
                    Unit = "",
                    State = SensorState.CRITICAL,
                    Timestamp = now
                },
                (_, existing) =>
                {
                    existing.PostCode = postIndex1Based.ToString();
                    existing.Type = SensorType.Pressure;
                    existing.Value = 1;
                    existing.Unit = "";
                    existing.State = SensorState.CRITICAL;
                    existing.Timestamp = now;
                    return existing;
                });

            SensorReadingReceived?.Invoke(this, new SensorReadingEventArgs { Reading = reading });
            CriticalAlertRaised?.Invoke(this, new AlertEventArgs
            {
                SensorId = reading.SensorId,
                Message = $"P{postIndex1Based}: presence trigger",
                Value = reading.Value,
                Threshold = 1
            });
        }
        catch (Exception ex)
        {
            AddLog("BLUETOOTH", "ERROR", $"Failed to process line: {ex.Message}");
        }
    }

    private static bool TryParsePostIndex(string line, out int postIndex1Based)
    {
        postIndex1Based = 0;
        var trimmed = line.Trim();
        if (trimmed.Length == 0) return false;

        // JSON format
        if (trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;

                // Expected format from sensors:
                // {"poste":1,"etat":"piece _detectee"}
                // {"poste":3,"etat":"piece _detectee"}
                // Only treat it as a trigger when etat indicates a detection.
                if (root.TryGetProperty("etat", out var etatEl) && etatEl.ValueKind == JsonValueKind.String)
                {
                    var etat = etatEl.GetString();
                    if (!IsPieceDetected(etat))
                    {
                        return false;
                    }
                }

                if (TryGetInt(root, "poste", out postIndex1Based)
                    || TryGetInt(root, "post", out postIndex1Based)
                    || TryGetInt(root, "postIndex", out postIndex1Based)
                    || TryGetInt(root, "p", out postIndex1Based))
                {
                    return postIndex1Based is >= 1 and <= 3;
                }
            }
            catch
            {
                // fall through to regex parsing
            }
        }

        // Text formats: P1 / POST-01 / post=3 ...
        var match = Regex.Match(trimmed, @"(?i)\b(?:poste|post|p)\s*[-:=#]?\s*0*([1-3])\b");
        if (match.Success && int.TryParse(match.Groups[1].Value, out postIndex1Based))
        {
            return true;
        }

        // Bare number line: "1" / "3"
        if (int.TryParse(trimmed, out postIndex1Based))
        {
            return postIndex1Based is >= 1 and <= 3;
        }

        return false;
    }

    private static bool IsPieceDetected(string? etat)
    {
        if (string.IsNullOrWhiteSpace(etat))
        {
            // If etat is missing, assume it's a detection (backward compatible).
            return true;
        }

        // Normalize: remove spaces/underscores and compare loosely.
        var normalized = etat
            .Trim()
            .Replace(" ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("_", "", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();

        // Accept common variants: "piece_detectee", "piece detectee", "piece_detected", etc.
        return normalized.Contains("piece", StringComparison.OrdinalIgnoreCase)
               && (normalized.Contains("detect", StringComparison.OrdinalIgnoreCase)
                   || normalized.Contains("detecte", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryGetInt(JsonElement root, string propertyName, out int value)
    {
        value = 0;
        if (!root.TryGetProperty(propertyName, out var el)) return false;

        try
        {
            if (el.ValueKind == JsonValueKind.Number) return el.TryGetInt32(out value);
            if (el.ValueKind == JsonValueKind.String) return int.TryParse(el.GetString(), out value);
        }
        catch
        {
            // ignore
        }

        return false;
    }

    private void AddLog(string source, string level, string message)
    {
        LogEventAdded?.Invoke(this, new IotLogEventArgs
        {
            LogEvent = new IotLogEvent
            {
                Timestamp = DateTime.Now,
                Source = source,
                Level = level,
                Message = message
            }
        });
    }

    public void Dispose()
    {
        _ = DisconnectAsync();
    }
}
