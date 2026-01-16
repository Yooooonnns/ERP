using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DigitalisationERP.Desktop.Models;

namespace DigitalisationERP.Desktop.Services.IoT;

/// <summary>
/// Simple provider that merges two providers into a single event stream.
/// Used to keep simulation running while overlaying real (Bluetooth) sensor triggers.
/// </summary>
public sealed class HybridIotProvider : IIotProvider, IDisposable
{
    private readonly IIotProvider _primary;
    private readonly IIotProvider _secondary;

    public string ProviderName => $"Hybrid({_primary.ProviderName}+{_secondary.ProviderName})";

    public bool IsConnected => _primary.IsConnected || _secondary.IsConnected;

    public event EventHandler<SensorReadingEventArgs>? SensorReadingReceived;
    public event EventHandler<RobotStateEventArgs>? RobotStateChanged;
    public event EventHandler<IotLogEventArgs>? LogEventAdded;
    public event EventHandler<AlertEventArgs>? CriticalAlertRaised;

    public HybridIotProvider(IIotProvider primary, IIotProvider secondary)
    {
        _primary = primary ?? throw new ArgumentNullException(nameof(primary));
        _secondary = secondary ?? throw new ArgumentNullException(nameof(secondary));

        _primary.SensorReadingReceived += ForwardSensor;
        _secondary.SensorReadingReceived += ForwardSensor;

        _primary.RobotStateChanged += ForwardRobot;
        _secondary.RobotStateChanged += ForwardRobot;

        _primary.LogEventAdded += ForwardLog;
        _secondary.LogEventAdded += ForwardLog;

        _primary.CriticalAlertRaised += ForwardAlert;
        _secondary.CriticalAlertRaised += ForwardAlert;
    }

    public async Task<bool> ConnectAsync()
    {
        bool primaryConnected = false;
        bool secondaryConnected = false;

        try { primaryConnected = await _primary.ConnectAsync().ConfigureAwait(false); } catch { /* ignore */ }
        try { secondaryConnected = await _secondary.ConnectAsync().ConfigureAwait(false); } catch { /* ignore */ }

        return primaryConnected || secondaryConnected;
    }

    public async Task DisconnectAsync()
    {
        try { await _secondary.DisconnectAsync().ConfigureAwait(false); } catch { /* ignore */ }
        try { await _primary.DisconnectAsync().ConfigureAwait(false); } catch { /* ignore */ }
    }

    public async Task<IotSensorReading?> ReadSensorAsync(string sensorId)
    {
        var fromSecondary = await _secondary.ReadSensorAsync(sensorId).ConfigureAwait(false);
        if (fromSecondary != null) return fromSecondary;
        return await _primary.ReadSensorAsync(sensorId).ConfigureAwait(false);
    }

    public Task<bool> SendRobotCommandAsync(string robotId, RobotCommand command, string? targetLocation = null)
    {
        // Prefer primary (simulation implements robot commands).
        return _primary.SendRobotCommandAsync(robotId, command, targetLocation);
    }

    public async Task<RobotState?> GetRobotStateAsync(string robotId)
    {
        var fromPrimary = await _primary.GetRobotStateAsync(robotId).ConfigureAwait(false);
        if (fromPrimary != null) return fromPrimary;
        return await _secondary.GetRobotStateAsync(robotId).ConfigureAwait(false);
    }

    public async Task<IotSensorReading[]> GetAllSensorsAsync()
    {
        var a = await _primary.GetAllSensorsAsync().ConfigureAwait(false);
        var b = await _secondary.GetAllSensorsAsync().ConfigureAwait(false);

        // If IDs collide, prefer secondary.
        var map = new Dictionary<string, IotSensorReading>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in a) map[r.SensorId] = r;
        foreach (var r in b) map[r.SensorId] = r;
        return map.Values.ToArray();
    }

    public async Task<RobotState[]> GetAllRobotsAsync()
    {
        var a = await _primary.GetAllRobotsAsync().ConfigureAwait(false);
        var b = await _secondary.GetAllRobotsAsync().ConfigureAwait(false);
        return a.Concat(b).GroupBy(r => r.RobotId ?? string.Empty, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToArray();
    }

    public Task<bool> SetSensorThresholdAsync(string sensorId, double warningThreshold, double criticalThreshold)
    {
        // Apply to both; if one succeeds, report success.
        return SetThresholdInternalAsync(sensorId, warningThreshold, criticalThreshold);
    }

    private async Task<bool> SetThresholdInternalAsync(string sensorId, double warningThreshold, double criticalThreshold)
    {
        bool a = false;
        bool b = false;

        try { a = await _primary.SetSensorThresholdAsync(sensorId, warningThreshold, criticalThreshold).ConfigureAwait(false); } catch { /* ignore */ }
        try { b = await _secondary.SetSensorThresholdAsync(sensorId, warningThreshold, criticalThreshold).ConfigureAwait(false); } catch { /* ignore */ }

        return a || b;
    }

    private void ForwardSensor(object? sender, SensorReadingEventArgs e) => SensorReadingReceived?.Invoke(sender, e);
    private void ForwardRobot(object? sender, RobotStateEventArgs e) => RobotStateChanged?.Invoke(sender, e);
    private void ForwardLog(object? sender, IotLogEventArgs e) => LogEventAdded?.Invoke(sender, e);
    private void ForwardAlert(object? sender, AlertEventArgs e) => CriticalAlertRaised?.Invoke(sender, e);

    public void Dispose()
    {
        _primary.SensorReadingReceived -= ForwardSensor;
        _secondary.SensorReadingReceived -= ForwardSensor;

        _primary.RobotStateChanged -= ForwardRobot;
        _secondary.RobotStateChanged -= ForwardRobot;

        _primary.LogEventAdded -= ForwardLog;
        _secondary.LogEventAdded -= ForwardLog;

        _primary.CriticalAlertRaised -= ForwardAlert;
        _secondary.CriticalAlertRaised -= ForwardAlert;

        try { (_secondary as IDisposable)?.Dispose(); } catch { /* ignore */ }
        try { (_primary as IDisposable)?.Dispose(); } catch { /* ignore */ }
    }
}
