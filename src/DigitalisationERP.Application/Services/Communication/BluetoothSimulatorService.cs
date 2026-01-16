using DigitalisationERP.Core.Abstractions.Communication;

namespace DigitalisationERP.Application.Services.Communication;

/// <summary>
/// Bluetooth/IoT device communication simulator for testing
/// </summary>
public class BluetoothSimulatorService : IBluetoothSimulator, IDeviceCommunicationService
{
    private readonly Dictionary<string, bool> _connectedDevices = new();
    private readonly Random _random = new();

    public async Task<IEnumerable<DeviceInfo>> SimulateDeviceScanAsync(int deviceCount = 5)
    {
        await Task.Delay(500); // Simulate scan time

        var devices = new List<DeviceInfo>();
        var deviceTypes = new[] { "Temperature Sensor", "Pressure Sensor", "Vibration Sensor", "Flow Meter", "Level Sensor" };

        for (int i = 0; i < deviceCount; i++)
        {
            devices.Add(new DeviceInfo(
                DeviceId: $"BT-{Guid.NewGuid():N}".Substring(0, 16),
                DeviceName: $"{deviceTypes[i % deviceTypes.Length]} #{i + 1}",
                DeviceType: deviceTypes[i % deviceTypes.Length],
                SignalStrength: _random.Next(-80, -30),
                IsAvailable: true
            ));
        }

        return devices;
    }

    public Task<SensorData> SimulateSensorDataAsync(string deviceId)
    {
        var sensorTypes = new[] { "Temperature", "Pressure", "Vibration", "Flow", "Level" };
        var sensorType = sensorTypes[_random.Next(sensorTypes.Length)];

        var data = sensorType switch
        {
            "Temperature" => new SensorData(deviceId, sensorType, _random.Next(15, 85), "°C", DateTime.UtcNow),
            "Pressure" => new SensorData(deviceId, sensorType, _random.Next(1, 10), "bar", DateTime.UtcNow),
            "Vibration" => new SensorData(deviceId, sensorType, _random.NextDouble() * 10, "mm/s", DateTime.UtcNow),
            "Flow" => new SensorData(deviceId, sensorType, _random.Next(50, 500), "L/min", DateTime.UtcNow),
            "Level" => new SensorData(deviceId, sensorType, _random.Next(0, 100), "%", DateTime.UtcNow),
            _ => new SensorData(deviceId, "Unknown", 0, "", DateTime.UtcNow)
        };

        return Task.FromResult(data);
    }

    public int SimulateSignalStrength(string deviceId)
    {
        // Simulate signal fluctuation between -80 dBm (weak) and -30 dBm (strong)
        return _random.Next(-80, -30);
    }

    // IDeviceCommunicationService implementation
    public Task<bool> ConnectAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (!_connectedDevices.ContainsKey(deviceId))
        {
            _connectedDevices[deviceId] = true;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task DisconnectAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        _connectedDevices.Remove(deviceId);
        return Task.CompletedTask;
    }

    public Task<bool> SendDataAsync(string deviceId, byte[] data, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_connectedDevices.ContainsKey(deviceId));
    }

    public async Task<byte[]?> ReceiveDataAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (!_connectedDevices.ContainsKey(deviceId))
            return null;

        var sensorData = await SimulateSensorDataAsync(deviceId);
        var jsonData = System.Text.Json.JsonSerializer.Serialize(sensorData);
        return System.Text.Encoding.UTF8.GetBytes(jsonData);
    }

    public bool IsConnected(string deviceId)
    {
        return _connectedDevices.ContainsKey(deviceId);
    }

    public async Task<IEnumerable<DeviceInfo>> ScanDevicesAsync(CancellationToken cancellationToken = default)
    {
        return await SimulateDeviceScanAsync();
    }
}
