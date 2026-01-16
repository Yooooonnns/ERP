namespace DigitalisationERP.Core.Abstractions.Communication;

/// <summary>
/// Bluetooth signal simulator for testing
/// </summary>
public interface IBluetoothSimulator
{
    /// <summary>
    /// Simulate device discovery
    /// </summary>
    Task<IEnumerable<DeviceInfo>> SimulateDeviceScanAsync(int deviceCount = 5);

    /// <summary>
    /// Simulate sensor data from a device
    /// </summary>
    Task<SensorData> SimulateSensorDataAsync(string deviceId);

    /// <summary>
    /// Simulate signal strength fluctuation
    /// </summary>
    int SimulateSignalStrength(string deviceId);
}

/// <summary>
/// Sensor data from IoT device
/// </summary>
public record SensorData(
    string DeviceId,
    string SensorType,
    double Value,
    string Unit,
    DateTime Timestamp,
    Dictionary<string, object>? Metadata = null);
